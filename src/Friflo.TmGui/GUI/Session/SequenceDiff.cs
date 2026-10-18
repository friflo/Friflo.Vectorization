// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Diagnostics.DebuggerBrowsableState;
using Browse = System.Diagnostics.DebuggerBrowsableAttribute;

// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable ConvertToAutoProperty
// ReSharper disable ReplaceWithFieldKeyword
// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;


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


public static partial class SequenceDiff
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool TryComputeChanges(
        ReadOnlySpan<int>   startState,
        ReadOnlySpan<int>   targetState,
        int                 maxDiffValueCount,
        int                 lookahead, // typical values: 64, 128
        List<SeqChange>     changes,
        out int             diffValueCount)
    {
        changes.Clear();
        diffValueCount = 0;

        int lenA = startState.Length, lenB = targetState.Length;
        if (lenA == 0 && lenB == 0) return true;

        int i = 0, j = 0;

        while (i < lenA && j < lenB)
        {
            // 1. Fast scalar skip for identical element sequences
            while (i < lenA && j < lenB && startState[i] == targetState[j]) { i++; j++; }
            if (i >= lenA || j >= lenB) break;

            // 2. Check for pure Insert
            int maxInsertScan = Math.Min(lookahead, lenB - j - 1);
            if (maxInsertScan > 0)
            {
                int targetIdx = targetState.Slice(j + 1, maxInsertScan).IndexOf(startState[i]);
                if (targetIdx != -1)
                {
                    int insertLen = targetIdx + 1;
                    if (diffValueCount + insertLen > maxDiffValueCount) goto Fail;
                    changes.Add(new SeqChange(SeqChangeType.Insert, i, insertLen));
                    diffValueCount += insertLen;
                    j += insertLen;
                    continue;
                }
            }

            // 3. Check for pure Remove
            int maxRemoveScan = Math.Min(lookahead, lenA - i - 1);
            if (maxRemoveScan > 0)
            {
                int startIdx = startState.Slice(i + 1, maxRemoveScan).IndexOf(targetState[j]);
                if (startIdx != -1)
                {
                    int removeLen = startIdx + 1;
                    changes.Add(new SeqChange(SeqChangeType.Remove, i, removeLen));
                    i += removeLen;
                    continue;
                }
            }

            // 4. Find resynchronization point for modifications / combined edits
            int syncA = -1, syncB = -1;
            int maxScanA = Math.Min(lookahead, lenA - i);
            int maxScanB = Math.Min(lookahead, lenB - j);

            // Search forward for the best alignment match
            for (int bi = 1; bi < maxScanB; bi++)
            {
                int val = targetState[j + bi];
                int matchInA = -1;

                // Prefer matches further down if multiple exist to handle grouped changes properly
                for (int ai = maxScanA - 1; ai >= 0; ai--)
                {
                    if (startState[i + ai] == val)
                    {
                        // Ensure we don't match an element that is too close if a better sync exists further ahead
                        matchInA = ai;
                        if (ai >= bi) break; 
                    }
                }

                if (matchInA != -1)
                {
                    syncA = i + matchInA;
                    syncB = j + bi;
                    break;
                }
            }

            if (syncA != -1)
            {
                int diffA = syncA - i;
                int diffB = syncB - j;

                if (diffA == diffB)
                {
                    if (diffValueCount + diffA > maxDiffValueCount) goto Fail;
                    changes.Add(new SeqChange(SeqChangeType.Modify, i, diffA));
                    diffValueCount += diffA;
                }
                else if (diffA < diffB)
                {
                    int modLen = diffA;
                    int insLen = diffB - diffA;
                    if (diffValueCount + modLen + insLen > maxDiffValueCount) goto Fail;
                    if (modLen > 0)
                    {
                        changes.Add(new SeqChange(SeqChangeType.Modify, i, modLen));
                        diffValueCount += modLen;
                    }
                    changes.Add(new SeqChange(SeqChangeType.Insert, i + modLen, insLen));
                    diffValueCount += insLen;
                }
                else
                {
                    int modLen = diffB;
                    int remLen = diffA - diffB;
                    if (diffValueCount + modLen > maxDiffValueCount) goto Fail;
                    if (modLen > 0)
                    {
                        changes.Add(new SeqChange(SeqChangeType.Modify, i, modLen));
                        diffValueCount += modLen;
                    }
                    changes.Add(new SeqChange(SeqChangeType.Remove, i + modLen, remLen));
                }

                i = syncA;
                j = syncB;
            }
            else
            {
                // Fallback: consume until end or mismatch
                int startI = i;
                while (i < lenA && j < lenB && startState[i] != targetState[j]) { i++; j++; }
                int modifyLen = i - startI;
                if (diffValueCount + modifyLen > maxDiffValueCount) goto Fail;
                changes.Add(new SeqChange(SeqChangeType.Modify, startI, modifyLen));
                diffValueCount += modifyLen;
            }
        }

        // 5. Process trailing elements
        if (i < lenA)
        {
            changes.Add(new SeqChange(SeqChangeType.Remove, i, lenA - i));
        }

        if (j < lenB)
        {
            int insertLen = lenB - j;
            if (diffValueCount + insertLen > maxDiffValueCount) goto Fail;
            changes.Add(new SeqChange(SeqChangeType.Insert, i, insertLen));
            diffValueCount += insertLen;
        }

        return true;

    Fail:
        changes.Clear();
        diffValueCount = -1;
        return false;
    }
}