// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;


internal readonly struct BufferRange : IComparable<BufferRange>
{
    internal readonly int bufferId;
    internal readonly int start;
    internal readonly int length;

    public override string ToString() => $"bufferI: {bufferId}  start: {start}:  length={length}";

    public int CompareTo(BufferRange other) => start.CompareTo(other.start);

    internal BufferRange(int bufferId, int start, int length)
    {
        this.bufferId   = bufferId;
        this.start      = start;
        this.length     = length;
    }
    
    internal static List<BufferRange> GetOptimizedRanges(List<BufferRange> requestedRanges, List<BufferRange> optimizedRanges)
    {
        optimizedRanges.Clear();
        
        if (requestedRanges.Count == 0) return optimizedRanges;
        if (requestedRanges.Count == 1) {
            optimizedRanges.Add(requestedRanges[0]);
            return optimizedRanges;
        }
        var span = CollectionsMarshal.AsSpan(requestedRanges);
        span.Sort(); // sort by Offset (start) index

        var current = requestedRanges[0];

        for (int i = 1; i < requestedRanges.Count; i++) {
            var next = requestedRanges[i];
            // do ranges overlap (e.g. [0,10] und [10,20])
            if (next.start <= current.start + current.length) {
                int currentEnd  = current.start + current.length;
                int nextEnd     = next.start + next.length;
                int newEnd      = Math.Max(currentEnd, nextEnd);
                
                current = new BufferRange(current.bufferId, current.start, newEnd - current.start);
            } else {
                optimizedRanges.Add(current);
                current = next;
            }
        }
        optimizedRanges.Add(current);
        return optimizedRanges;
    }
}

internal readonly struct BufferEntry
{
    internal readonly   IWgpuBuffer                             wgpuBuffer;
    internal readonly   List<BufferRange>                       requestedRanges;
    internal readonly   Dictionary<SegmentKey, SegmentState>    bufferSegments;

    public   override   string      ToString() => wgpuBuffer?.ToString();

    internal BufferEntry(IWgpuBuffer wgpuBuffer) {
        this.wgpuBuffer = wgpuBuffer;
        requestedRanges = new List<BufferRange>();
        bufferSegments  = new Dictionary<SegmentKey, SegmentState>();
    }
}

internal unsafe struct BufferData
{
    internal readonly   IWgpuBuffer         wgpu;
    internal readonly   int                 elementSize;
    internal readonly   int                 length;
    internal readonly   Buffer*             storageHandle;
    internal            Buffer*             stagingHandle;
    internal            List<BufferRange>   requestedRanges;
    
    internal BufferData(IWgpuBuffer wgpu, int elementSize, int length, Buffer* storageHandle, Buffer* stagingHandle) {
        this.wgpu           = wgpu;
        this.elementSize    = elementSize;
        this.length         = length;
        this.storageHandle  = storageHandle;
        this.stagingHandle  = stagingHandle;
    }
}