// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InvertIf
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;

internal struct SegmentState
{
    internal int  kernelId;    // last kernel seq
    internal int  kernelSeq;    // last kernel seq
    internal bool isWrite;      // true = Write, false = Read
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

    public override bool Equals(object obj) {
        return obj is SegmentKey other && Equals(other);
    }

    public override int GetHashCode() {
        return HashCode.Combine(start, length);
    }

    // optional operator for fast comparison in code
    public static bool operator ==(SegmentKey left, SegmentKey right) => left.Equals(right);
    public static bool operator !=(SegmentKey left, SegmentKey right) => !left.Equals(right);
    
    // Important: segmentMap MUST be cleared at wgpuQueueSubmit()
    [StackTraceHidden]
    internal static bool AddRead(Dictionary<SegmentKey, SegmentState> segmentMap, SegmentKey key, int kernelId, int kernelSeq, string param)
    {
        ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(segmentMap, key, out bool exists);
        if (exists) {
            if (state.kernelSeq == kernelSeq) {
                if (state.isWrite) {
                    ThrowConflictingUsages(param);
                }
                return false;
            }
            bool pipelineChanged    = state.kernelId != kernelId;
            bool hasConflict        = state.isWrite || pipelineChanged;
            state.kernelSeq = kernelSeq;
            state.kernelId  = kernelId;
            state.isWrite   = false;
            return hasConflict;
        }
        state.kernelSeq = kernelSeq;
        state.kernelId  = kernelId;
        state.isWrite   = false;
        return false;
    }
    
    // Important: segmentMap MUST be cleared at wgpuQueueSubmit()
    [StackTraceHidden]
    internal static bool AddReadWrite(Dictionary<SegmentKey, SegmentState> segmentMap, SegmentKey key, int kernelId, int kernelSeq, string param)
    {
        ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(segmentMap, key, out bool exists);
        if (exists) {
            if (state.kernelSeq == kernelSeq) {
                ThrowConflictingUsages(param);
                return false;
            }
            bool pipelineChanged   =  state.kernelId != kernelId;
            bool hasConflict       = !state.isWrite || pipelineChanged;
            state.kernelSeq = kernelSeq;
            state.kernelId  = kernelId;
            state.isWrite   = true;
            return hasConflict;
        }
        state.kernelSeq = kernelSeq;
        state.kernelId  = kernelId;
        state.isWrite   = true;
        return false;
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)][StackTraceHidden][DoesNotReturn]
    private static void ThrowConflictingUsages(string param)
    {
        throw new InvalidOperationException($"Schrödinger's Buffer: Parameter '{param}' is suffering from a temporal personality split. " +
            $"You are trying to read from it and write to it within the EXACT SAME kernel execution. Pick a side, time traveler!");
    }
}