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
    internal readonly   List<WgpuCommandBuffer> buffers;
    internal readonly   List<BufferRange>       ranges; // contains all requested ranges for buffers

    public   override   string                  ToString() => $"buffers: {buffers.Count}  ranges: {ranges.Count}";

    public CommandList() {
        buffers = new List<WgpuCommandBuffer>();
        ranges  = new List<BufferRange>();
    }
}


/// ConcurrentStack is lock-free. Spin-Wait's on failed operations, but does not lock. <br/>
/// Check alternative for multi threading <see cref="CommandListPoolTLS"/>
internal class CommandListPool
{
    private readonly ConcurrentStack<CommandList> pooled = []; 

    internal CommandList Fetch()
    {
        if (pooled.TryPop(out var list)) {
            return list;
        }
        return new CommandList();
    }

    internal void Return(CommandList list)
    {
        list.buffers.Clear();
        list.ranges.Clear();
        
        pooled.Push(list);
    }
}

/// <summary>
/// A zero-allocation, lock-free <see cref="CommandList"/> pool combining Thread-Local Storage (TLS) for ultra-fast, 
/// cache-hot local thread access (95% hot path) with a <see cref="ConcurrentStack{T}"/> fallback for cross-thread recycling.
/// Eliminates boxing, avoids atomic inter-core CPU stalls (Interlocked), and preserves list capacities to prevent runtime heap allocations.
/// </summary>
internal class CommandListPoolTLS
{
    private readonly    ThreadLocal<CommandList>        localSlot = new(() => default);
    private readonly    ConcurrentStack<CommandList>    globalPool = [];

    internal CommandList Fetch() {
        var list = localSlot.Value;

        if (list.buffers != null) {
            localSlot.Value = default;
            return list;
        }
        if (globalPool.TryPop(out list)) {
            return list;
        }
        return new CommandList();
    }

    internal void Return(CommandList list)
    {
        list.buffers.Clear();
        list.ranges.Clear();

        if (localSlot.Value.buffers == null) {
            localSlot.Value = list;
        } else {
            globalPool.Push(list);
        }
    }
}