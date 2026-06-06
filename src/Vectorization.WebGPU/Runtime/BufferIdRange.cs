// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;


internal readonly struct BufferIdRange
{
    internal readonly   uint    bufferId;
    internal readonly   int     start;
    internal readonly   int     length;

    public   override   string  ToString() => $"bufferId: {bufferId}  [{start}..{start + length}]";

    internal BufferIdRange(uint bufferId, int start, int length)
    {
        this.bufferId   = bufferId;
        this.start      = start;
        this.length     = length;
    }
}

internal readonly struct BufferRange
{
    internal readonly   int     start;
    internal readonly   int     length;

    public   override   string  ToString() => $"[{start}..{start + length}]";

    internal BufferRange(int start, int length)
    {
        this.start  = start;
        this.length = length;
    }
    
    internal static void GetOptimizedRanges(List<BufferRange> requestedRanges, List<BufferRange> optimizedRanges)
    {
        optimizedRanges.Clear();
        
        if (requestedRanges.Count == 1) {
            optimizedRanges.Add(requestedRanges[0]);
            return;
        }
        var span = CollectionsMarshal.AsSpan(requestedRanges);
        span.Sort((a, b) => a.start.CompareTo(b.start)); // sort by Offset (start) index

        var current = span[0];

        for (int i = 1; i < span.Length; i++)
        {
            var next = span[i];
            // do ranges overlap (e.g. [0,10] und [10,20])
            if (next.start <= current.start + current.length)
            {
                int currentEnd  = current.start + current.length;
                int nextEnd     = next.start    + next.length;
                int newEnd      = Math.Max(currentEnd, nextEnd);
                
                current = new BufferRange(current.start, newEnd - current.start);
            } else {
                optimizedRanges.Add(current);
                current = next;
            }
        }
        optimizedRanges.Add(current);
    }
}

internal readonly struct BufferEntry
{
    internal readonly   uint                bufferId;
    internal readonly   List<BufferRange>   requestedRanges;
    internal readonly   SegmentMap          bufferSegments;

    public override string ToString() => bufferSegments == null ? null : $"bufferId: {bufferId}  segments: {bufferSegments.Count}  ranges: {requestedRanges.Count}";

    internal BufferEntry(uint bufferId) {
        this.bufferId       = bufferId;
        requestedRanges     = new List<BufferRange>();
        bufferSegments      = new SegmentMap();
    }
}

internal readonly unsafe struct BufferData
{
    internal readonly   int     bufferId;
    internal readonly   int     elementSize;
    internal readonly   Buffer* storageHandle;

    public   override   string  ToString() => $"bufferId: {bufferId}";

    internal BufferData(int bufferId, int elementSize, Buffer* storageHandle) {
        this.bufferId       = bufferId;
        this.elementSize    = elementSize;
        this.storageHandle  = storageHandle;
    }
}

internal readonly struct ActiveBuffer
{
    internal readonly   BufferData          data;
    internal readonly   List<BufferRange>   compactRanges;

    public   override   string              ToString() => $"bufferId: {data.bufferId}  ranges: {compactRanges.Count}";

    internal ActiveBuffer(in BufferData data, List<BufferRange> compactRanges) {
        this.data           = data;
        this.compactRanges  = compactRanges;
    }
}