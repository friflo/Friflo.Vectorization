// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
namespace Friflo.Vectorization.WebGPU.Runtime;

internal struct CommandList
{
    internal readonly   Queue<WgpuCommandBuffer>    buffers;
    internal readonly   List<BufferRange>           ranges; // contains all requested ranges for buffers
    
    public CommandList() {
        buffers = new Queue<WgpuCommandBuffer>();
        ranges  = new List<BufferRange>();
    }
}


/// ConcurrentStack is lock-free. Spin-Wait's on failed operations, but does not lock
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