// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;

[Flags]
internal enum SegmentState : byte
{
    None        = 0,
    Read        = 1 << 0, // 1 - is currently read by CPU
    Write       = 1 << 1, // 2 - is currently written by CPU
    
//  PendingDownload = 1 << 2  // 4 - maybe used to track download
}

internal readonly struct SegmentKey : IEquatable<SegmentKey>
{
    private readonly    int start;
    private readonly    int length;
    
    internal SegmentKey(int start, int length) {
        this.length = length;
        this.start  = start;
    }
    
    public bool Equals(SegmentKey other) {
        return start == other.start && length == other.length;
    }

    public override bool Equals(object? obj) {
        return obj is SegmentKey other && Equals(other);
    }

    public override int GetHashCode() {
        return HashCode.Combine(start, length);
    }

    // optional operator for fast comparison in code
    public static bool operator ==(SegmentKey left, SegmentKey right) => left.Equals(right);
    public static bool operator !=(SegmentKey left, SegmentKey right) => !left.Equals(right);
    
    internal static bool AddRead(Dictionary<SegmentKey, SegmentState> segmentMap, SegmentKey key)
    {
        return false;
    }
    
    internal static bool AddReadWrite(Dictionary<SegmentKey, SegmentState> segmentMap, SegmentKey key)
    {
        return false;
    }

}