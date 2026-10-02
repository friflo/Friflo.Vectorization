// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static System.Diagnostics.DebuggerBrowsableState;
using Browse = System.Diagnostics.DebuggerBrowsableAttribute;

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
    public static bool TryComputeChanges(
        ReadOnlySpan<int>   startState,
        ReadOnlySpan<int>   targetState,
        int                 maxOperations,
        List<SeqChange>     changes)
    {
        changes.Clear();

        int i = 0; // Index in startState
        int j = 0; // Index in targetState

        ReadOnlySpan<byte> bytesStart = MemoryMarshal.AsBytes(startState);
        ReadOnlySpan<byte> bytesTarget = MemoryMarshal.AsBytes(targetState);
        const int structSize = sizeof(int);

        const int lookaheadWindow = 8; // Window size to search for stream resynchronization

        while (i < startState.Length && j < targetState.Length)
        {
            // 1. Fast SIMD skip over identical int sequences
            int commonBytes = bytesStart.Slice(i * structSize).CommonPrefixLength(bytesTarget.Slice(j * structSize));
            int commonStructs = commonBytes / structSize;

            i += commonStructs;
            j += commonStructs;

            if (i >= startState.Length && j >= targetState.Length)
            {
                break;
            }

            // Early exit check before processing new change
            if (changes.Count >= maxOperations)
            {
                changes.Clear();
                return false;
            }

            // 2. Resynchronization check via lookahead window
            int bestOffsetStart = -1;
            int bestOffsetTarget = -1;

            for (int offset = 1; offset <= lookaheadWindow; offset++)
            {
                // Check for Insert (Target has inserted items)
                if (j + offset < targetState.Length && startState[i] == targetState[j + offset])
                {
                    bestOffsetStart = 0;
                    bestOffsetTarget = offset;
                    break;
                }

                // Check for Remove (Start has removed items)
                if (i + offset < startState.Length && startState[i + offset] == targetState[j])
                {
                    bestOffsetStart = offset;
                    bestOffsetTarget = 0;
                    break;
                }
            }

            // 3. Emit matching Change operation
            if (bestOffsetTarget > 0 && bestOffsetStart == 0)
            {
                // Insert operation
                int len = bestOffsetTarget;
                changes.Add(new SeqChange(SeqChangeType.Insert, i, len));
                j += len;
            }
            else if (bestOffsetStart > 0 && bestOffsetTarget == 0)
            {
                // Remove operation
                int len = bestOffsetStart;
                changes.Add(new SeqChange(SeqChangeType.Remove, i, len));
                i += len;
            }
            else
            {
                // Modify operation (determine length of consecutive modified items)
                int modifyStartI = i;

                while (i < startState.Length && j < targetState.Length && startState[i] != targetState[j])
                {
                    i++;
                    j++;

                    // Stop if resync is possible ahead
                    if (i < startState.Length && j < targetState.Length)
                    {
                        if (bytesStart.Slice(i * structSize).CommonPrefixLength(bytesTarget.Slice(j * structSize)) > 0)
                        {
                            break;
                        }
                    }
                }

                int len = i - modifyStartI;
                changes.Add(new SeqChange(SeqChangeType.Modify, modifyStartI, len));
            }
        }

        // 4. Process remaining trailing elements
        if (i < startState.Length)
        {
            if (changes.Count >= maxOperations)
            {
                changes.Clear();
                return false;
            }

            changes.Add(new SeqChange(SeqChangeType.Remove, i, startState.Length - i));
        }
        else if (j < targetState.Length)
        {
            if (changes.Count >= maxOperations)
            {
                changes.Clear();
                return false;
            }

            changes.Add(new SeqChange(SeqChangeType.Insert, i, targetState.Length - j));
        }

        return changes.Count <= maxOperations;
    }
}