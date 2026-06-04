// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;


internal readonly struct BufferIdRange : IComparable<BufferIdRange>
{
    internal readonly   uint    bufferId;
    internal readonly   int     start;
    internal readonly   int     length;

    public   override   string  ToString() => $"bufferId: {bufferId}  [{start}..{start + length}]";

    public int CompareTo(BufferIdRange other) => start.CompareTo(other.start);

    internal BufferIdRange(uint bufferId, int start, int length)
    {
        this.bufferId   = bufferId;
        this.start      = start;
        this.length     = length;
    }
    
    internal static List<BufferIdRange> GetOptimizedRanges(List<BufferIdRange> requestedRanges, List<BufferIdRange> optimizedRanges)
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
                
                current = new BufferIdRange(current.bufferId, current.start, newEnd - current.start);
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
    internal readonly   uint                bufferId;
    internal readonly   List<BufferIdRange> requestedIdRanges;
    internal readonly   SegmentMap          bufferSegments;

    public override string ToString() => bufferSegments == null ? null : $"bufferId: {bufferId}  segments: {bufferSegments.Count}  ranges: {requestedIdRanges.Count}";

    internal BufferEntry(uint bufferId) {
        this.bufferId       = bufferId;
        requestedIdRanges   = new List<BufferIdRange>();
        bufferSegments      = new SegmentMap();
    }
    
    internal void Clear()
    {
        if (requestedIdRanges == null) {
            return;
        }
        requestedIdRanges.Clear();
        bufferSegments.Clear();
    }
}

internal readonly unsafe struct BufferData
{
    internal readonly   int                 bufferId;
    internal readonly   int                 elementSize;
    internal readonly   int                 length;
    internal readonly   Buffer*             storageHandle;
    internal readonly   Buffer*             stagingHandle;

    public   override   string              ToString() => $"bufferId: {bufferId}  length: {length}";

    internal BufferData(int bufferId, int elementSize, int length, Buffer* storageHandle, Buffer* stagingHandle) {
        this.bufferId       = bufferId;
        this.elementSize    = elementSize;
        this.length         = length;
        this.storageHandle  = storageHandle;
        this.stagingHandle  = stagingHandle;
    }
}