// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    [Browse(Never)] [FieldOffset(0)] private readonly   int         start;
    [Browse(Never)] [FieldOffset(3)] private readonly   SeqChangeType  type;
    [Browse(Never)] [FieldOffset(4)] private readonly   int         length;
    
                    public  int         Start   => start & 0x00ffffff;
                    public  SeqChangeType  Type    => type;
                    public  int         Length  => length;

    [Browse(Never)] public  string      Label => $"{type} [{Start}..{Length}]";
    
    public override         string      ToString() => Label;

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
        ReadOnlySpan<int>   startState,
        ReadOnlySpan<int>   targetState,
        int                 maxOperations,
        List<SeqChange>     changes)
    {
        changes.Clear();

        int i = 0;
        int j = 0;

        int lenA = startState.Length;
        int lenB = targetState.Length;

        ReadOnlySpan<byte> bytesStart = MemoryMarshal.AsBytes(startState);
        ReadOnlySpan<byte> bytesTarget = MemoryMarshal.AsBytes(targetState);
        const int structSize = sizeof(int);
        const int lookaheadWindow = 8;

        while (i < lenA && j < lenB)
        {
            // 1. Fast SIMD skip over identical sequences
            int remainingA = (lenA - i) * structSize;
            int remainingB = (lenB - j) * structSize;

            int commonBytes = bytesStart.Slice(i * structSize, remainingA)
                                        .CommonPrefixLength(bytesTarget.Slice(j * structSize, remainingB));
            
            int commonStructs = commonBytes / structSize;

            i += commonStructs;
            j += commonStructs;

            if (i >= lenA && j >= lenB)
            {
                break;
            }

            if (changes.Count >= maxOperations)
            {
                changes.Clear();
                return false;
            }

            int valA = i < lenA ? startState[i] : 0;
            int valB = j < lenB ? targetState[j] : 0;

            // 2. Resynchronization search
            int bestOffsetStart = -1;
            int bestOffsetTarget = -1;

            int maxOffset = Math.Min(lookaheadWindow, Math.Max(lenA - i, lenB - j));

            for (int offset = 1; offset < maxOffset; offset++)
            {
                if (j + offset < lenB && valA == targetState[j + offset])
                {
                    bestOffsetStart = 0;
                    bestOffsetTarget = offset;
                    break;
                }

                if (i + offset < lenA && startState[i + offset] == valB)
                {
                    bestOffsetStart = offset;
                    bestOffsetTarget = 0;
                    break;
                }
            }

            // 3. Emit changes
            if (bestOffsetTarget > 0 && bestOffsetStart == 0)
            {
                int len = bestOffsetTarget;
                changes.Add(new SeqChange(SeqChangeType.Insert, i, len));
                j += len;
            }
            else if (bestOffsetStart > 0 && bestOffsetTarget == 0)
            {
                int len = bestOffsetStart;
                changes.Add(new SeqChange(SeqChangeType.Remove, i, len));
                i += len;
            }
            else
            {
                // Modify range detection
                int modifyStartI = i;

                while (i < lenA && j < lenB)
                {
                    if (startState[i] == targetState[j])
                    {
                        break;
                    }

                    i++;
                    j++;
                }

                int len = i - modifyStartI;
                if (len > 0)
                {
                    changes.Add(new SeqChange(SeqChangeType.Modify, modifyStartI, len));
                }
            }
        }

        // 4. Trailing elements
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