// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
namespace Friflo.Vectorization.WebGPU.Runtime;

internal struct CommandList
{
    internal readonly   List<WgpuCommandBuffer> commands;
    internal readonly   List<BufferRange>       ranges; // contains all requested ranges for buffers

    public   override   string                  ToString() => $"commands: {commands.Count}  ranges: {ranges.Count}";

    public CommandList() {
        commands    = new List<WgpuCommandBuffer>();
        ranges      = new List<BufferRange>();
    }
}


/// ConcurrentQueue is lock-free. Spin-Wait's on failed operations, but does not lock. <br/>
/// Check alternative for multi threading <see cref="CommandListPoolTLS"/>
internal class CommandListPool
{
    // used ConcurrentQueue<T> in favor of ConcurrentStack<T>
    private readonly ConcurrentQueue<CommandList> pooled = [];

    public  override    string  ToString() => $"pooled: {pooled.Count}";

    internal CommandList Fetch()
    {
        if (pooled.TryDequeue(out var list)) {
            return list;
        }
        return new CommandList();
    }

    internal void Return(CommandList list)
    {
        list.commands.Clear();
        list.ranges.Clear();
        
        pooled.Enqueue(list);
        // ConcurrentStack<CommandList>.Push() allocates Node -> 40 bytes 
    }
}

/// <summary>
/// A zero-allocation, lock-free <see cref="CommandList"/> pool combining Thread-Local Storage (TLS) for ultra-fast, 
/// cache-hot local thread access (95% hot path) with a <see cref="ConcurrentQueue{T}"/> fallback for cross-thread recycling.
/// Eliminates boxing, avoids atomic inter-core CPU stalls (Interlocked), and preserves list capacities to prevent runtime heap allocations.
/// </summary>
internal class CommandListPoolTLS
{
    // used ConcurrentQueue<T> in favor of ConcurrentStack<T>
    private readonly    ConcurrentQueue<CommandList>    globalPool = [];
    private readonly    ThreadLocal<CommandList>        localSlot = new(() => default);

    internal CommandList Fetch() {
        var list = localSlot.Value;

        if (list.commands != null) {
            localSlot.Value = default;
            return list;
        }
        if (globalPool.TryDequeue(out list)) {
            return list;
        }
        return new CommandList();
    }

    internal void Return(CommandList list)
    {
        list.commands.Clear();
        list.ranges.Clear();

        if (localSlot.Value.commands == null) {
            localSlot.Value = list;
        } else {
            globalPool.Enqueue(list);
            // ConcurrentStack<CommandList>.Push() allocates Node -> 40 bytes 
        }
    }
}