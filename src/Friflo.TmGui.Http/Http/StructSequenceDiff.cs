// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using static System.Diagnostics.DebuggerBrowsableState;
using Browse = System.Diagnostics.DebuggerBrowsableAttribute;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable ConvertToAutoProperty
// ReSharper disable ReplaceWithFieldKeyword
// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;


public enum SeqChangeType : byte
{
    None    = 0,
    Modify  = 1,
    Insert  = 2,
    Remove  = 3,
}

[StructLayout(LayoutKind.Explicit, Size = 8)]
public readonly struct SeqChange
{
    [Browse(Never)] [FieldOffset(0)] private readonly   int             start;
    [Browse(Never)] [FieldOffset(3)] private readonly   SeqChangeType   type;
    [Browse(Never)] [FieldOffset(4)] private readonly   int             length;
    
                    public  int             Start   => start & 0x00ffffff;
                    public  SeqChangeType   Type    => type;
                    public  int             Length  => length;

    [Browse(Never)] public  string          Label => $"{type} [{Start}..{Length}]";
    
    public override         string          ToString() => Label;

    public SeqChange(SeqChangeType type, int start, int length)
    {
        this.start  = start;
        this.type   = type;
        this.length = length;
    }
}


public static class SequenceDiff
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool TryComputeChanges(
        ReadOnlySpan<int> startState,
        ReadOnlySpan<int> targetState,
        int maxOperations,
        List<SeqChange> changes)
    {
        changes.Clear();

        int lenA = startState.Length;
        int lenB = targetState.Length;

        if (lenA == 0 && lenB == 0) return true;

        int i = 0;
        int j = 0;

        ReadOnlySpan<byte> bytesStart = MemoryMarshal.AsBytes(startState);
        ReadOnlySpan<byte> bytesTarget = MemoryMarshal.AsBytes(targetState);
        const int structSize = sizeof(int);

        while (i < lenA && j < lenB)
        {
            // 1. Fast SIMD skip over identical block sequences via CommonPrefixLength
            int remainingBytesA = (lenA - i) * structSize;
            int remainingBytesB = (lenB - j) * structSize;

            int commonBytes = bytesStart.Slice(i * structSize, remainingBytesA)
                                        .CommonPrefixLength(bytesTarget.Slice(j * structSize, remainingBytesB));

            int commonStructs = commonBytes / structSize;
            i += commonStructs;
            j += commonStructs;

            if (i >= lenA && j >= lenB) break;

            if (changes.Count >= maxOperations)
            {
                changes.Clear();
                return false;
            }

            int valA = startState[i];
            int valB = targetState[j];

            int matchOffsetStart = -1;
            int matchOffsetTarget = -1;

            // 2a. AVX2 Path (x86 - 8 Elements)
            if (Avx2.IsSupported)
            {
                if (j + 8 <= lenB)
                {
                    Vector256<int> targetVec = Vector256.Create(targetState.Slice(j, 8));
                    Vector256<int> valAVec = Vector256.Create(valA);

                    Vector256<int> cmpResult = Vector256.Equals(targetVec, valAVec);
                    uint mask = cmpResult.ExtractMostSignificantBits();

                    if (mask != 0)
                    {
                        matchOffsetStart = 0;
                        matchOffsetTarget = BitOperations.TrailingZeroCount(mask);
                    }
                }

                if (matchOffsetTarget == -1 && i + 8 <= lenA)
                {
                    Vector256<int> startVec = Vector256.Create(startState.Slice(i, 8));
                    Vector256<int> valBVec = Vector256.Create(valB);

                    Vector256<int> cmpResult = Vector256.Equals(startVec, valBVec);
                    uint mask = cmpResult.ExtractMostSignificantBits();

                    if (mask != 0)
                    {
                        matchOffsetStart = BitOperations.TrailingZeroCount(mask);
                        matchOffsetTarget = 0;
                    }
                }
            }
            // 2b. Cross-Platform Vector128 / ARM NEON Path (ARM64 & SSE2 - 4 Elements)
            else if (Vector128.IsHardwareAccelerated)
            {
                if (j + 4 <= lenB)
                {
                    Vector128<int> targetVec = Vector128.Create(targetState.Slice(j, 4));
                    Vector128<int> valAVec = Vector128.Create(valA);

                    Vector128<int> cmpResult = Vector128.Equals(targetVec, valAVec);
                    uint mask = cmpResult.ExtractMostSignificantBits();

                    if (mask != 0)
                    {
                        matchOffsetStart = 0;
                        matchOffsetTarget = BitOperations.TrailingZeroCount(mask);
                    }
                }

                if (matchOffsetTarget == -1 && i + 4 <= lenA)
                {
                    Vector128<int> startVec = Vector128.Create(startState.Slice(i, 4));
                    Vector128<int> valBVec = Vector128.Create(valB);

                    Vector128<int> cmpResult = Vector128.Equals(startVec, valBVec);
                    uint mask = cmpResult.ExtractMostSignificantBits();

                    if (mask != 0)
                    {
                        matchOffsetStart = BitOperations.TrailingZeroCount(mask);
                        matchOffsetTarget = 0;
                    }
                }
            }

            // Fallback scalar lookahead loop if SIMD didn't hit or boundary failed
            if (matchOffsetTarget == -1 && matchOffsetStart == -1)
            {
                int maxLookahead = Math.Min(8, Math.Max(lenA - i, lenB - j));
                for (int offset = 1; offset < maxLookahead; offset++)
                {
                    if (j + offset < lenB && valA == targetState[j + offset])
                    {
                        matchOffsetStart = 0;
                        matchOffsetTarget = offset;
                        break;
                    }
                    if (i + offset < lenA && startState[i + offset] == valB)
                    {
                        matchOffsetStart = offset;
                        matchOffsetTarget = 0;
                        break;
                    }
                }
            }

            // 3. Emit matching SeqChange operation
            if (matchOffsetTarget > 0 && matchOffsetStart == 0)
            {
                changes.Add(new SeqChange(SeqChangeType.Insert, i, matchOffsetTarget));
                j += matchOffsetTarget;
            }
            else if (matchOffsetStart > 0 && matchOffsetTarget == 0)
            {
                changes.Add(new SeqChange(SeqChangeType.Remove, i, matchOffsetStart));
                i += matchOffsetStart;
            }
            else
            {
                // Modify range scan
                int modifyStartI = i;

                while (i < lenA && j < lenB)
                {
                    if (startState[i] == targetState[j]) break;
                    i++;
                    j++;
                }

                int modifyLen = i - modifyStartI;
                if (modifyLen > 0)
                {
                    changes.Add(new SeqChange(SeqChangeType.Modify, modifyStartI, modifyLen));
                }
            }
        }

        // 4. Process trailing elements
        if (i < lenA)
        {
            if (changes.Count >= maxOperations)
            {
                changes.Clear();
                return false;
            }
            changes.Add(new SeqChange(SeqChangeType.Remove, i, lenA - i));
        }
        else if (j < lenB)
        {
            if (changes.Count >= maxOperations)
            {
                changes.Clear();
                return false;
            }
            changes.Add(new SeqChange(SeqChangeType.Insert, i, lenB - j));
        }

        return changes.Count <= maxOperations;
    }
}