// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;

// ReSharper disable ArrangeRedundantParentheses
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InvertIf
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;

internal struct SegmentState
{
    internal        int     kernelId;     // last kernel ID
    internal        int     kernelSeq;    // last kernel seq
    internal        bool    isWrite;      // true = Write, false = Read
    
    public override string  ToString() => $"kernelId: {kernelId}  kernelSeq: {kernelSeq}";
}

internal readonly struct SegmentKey : IEquatable<SegmentKey>
{
    private readonly    int     start;
    private readonly    int     length;
    
    public  override    string  ToString() => $"[{start}..{start + length}]";
    
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
}



public sealed partial class CommandRecorder
{
    // Important: segmentMap MUST be cleared at wgpuQueueSubmit()
    [MethodImpl(MethodImplOptions.NoInlining)] [StackTraceHidden]
    private bool AddRead(SegmentMap segmentMap, int offset, int length, int kernel, int seq, string param)
    {
        var key = new SegmentKey(offset, length);
        ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(segmentMap, key, out bool exists);
        bool hasConflict = false;
        if (exists) {
            if (state.kernelSeq == seq) {
                if (state.isWrite) {
                    throw ThrowConflictingUsages(param);
                }
            }
            hasConflict =   (state.kernelId != kernel)      // pipeline changed
                        ||   state.isWrite;                 // RAW - Read-After-Write
            if (hasConflict) {
                if (enableTraces) {
                    AddTrace(PipelineTraceType.Pass_Split_RAW, kernel, 0, 0, param);
                }
                pipelineStats.Hazards++;
            }
        }
        state.kernelSeq = seq;
        state.kernelId  = kernel;
        state.isWrite   = false;
        return hasConflict;
    }

    // Important: segmentMap MUST be cleared at wgpuQueueSubmit()
    [MethodImpl(MethodImplOptions.NoInlining)] [StackTraceHidden]
    private bool AddReadWrite(SegmentMap segmentMap, int offset, int length, int kernel, int seq, string param)
    {
        var key = new SegmentKey(offset, length);
        ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(segmentMap, key, out bool exists);
        bool hasConflict = false;
        if (exists) {
            if (state.kernelSeq == seq) {
                throw ThrowConflictingUsages(param);
            }
            hasConflict =  (state.kernelId != kernel)       // pipeline changed
                        || !state.isWrite;                  // WAR - Write-After-Read
            if (hasConflict) {
                if (enableTraces) {
                    AddTrace(state.isWrite ? PipelineTraceType.Pass_Split_WAW : PipelineTraceType.Pass_Split_WAR, kernel, 0, 0, param);
                }
                pipelineStats.Hazards++;
            }
        }
        state.kernelSeq = seq;
        state.kernelId  = kernel;
        state.isWrite   = true;
        return hasConflict;
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)][StackTraceHidden][DoesNotReturn]
    private static InvalidOperationException ThrowConflictingUsages(string param)
    {
        return new InvalidOperationException($"Schrödinger's Buffer: Parameter '{param}' is suffering from a temporal personality split. " +
            $"You are trying to read from it and write to it within the EXACT SAME kernel execution. Pick a side, time traveler!");
    }
}