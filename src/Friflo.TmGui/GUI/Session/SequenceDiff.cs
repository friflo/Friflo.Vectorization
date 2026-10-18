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

            int valA = startState[i], valB = targetState[j];
            int matchTarget = int.MaxValue;
            int matchStart  = int.MaxValue;

            // 2. Bounded SIMD-accelerated lookahead scan
            int maxB = Math.Min(lookahead, lenB - j - 1);
            if (maxB > 0) {
                int idx = targetState.Slice(j + 1, maxB).IndexOf(valA);
                if (idx != -1) matchTarget = idx + 1;
            }

            int maxA = Math.Min(lookahead, lenA - i - 1);
            if (maxA > 0) {
                int idx = startState.Slice(i + 1, maxA).IndexOf(valB);
                if (idx != -1) matchStart = idx + 1;
            }

            // 3. Emit matching SeqChange operation based on shortest distance
            if (matchTarget != int.MaxValue || matchStart != int.MaxValue)
            {
                if (matchTarget <= matchStart)
                {
                    if (diffValueCount + matchTarget > maxDiffValueCount) goto Fail;
                    changes.Add(new SeqChange(SeqChangeType.Insert, i, matchTarget));
                    diffValueCount += matchTarget;
                    j += matchTarget;
                }
                else
                {
                    changes.Add(new SeqChange(SeqChangeType.Remove, i, matchStart));
                    i += matchStart;
                }
            }
            else
            {
                // Check for Modify + Remove (targetState[j+1] found in startState lookahead)
                int removeMatch = -1;
                if (j + 1 < lenB)
                {
                    int maxRemA = Math.Min(lookahead, lenA - i - 1);
                    if (maxRemA > 0)
                    {
                        int idx = startState.Slice(i + 1, maxRemA).IndexOf(targetState[j + 1]);
                        if (idx != -1) removeMatch = i + 1 + idx;
                    }
                }

                // Check for Modify + Insert (startState[i+1] found in targetState lookahead)
                int insertMatch = -1;
                if (i + 1 < lenA)
                {
                    int maxInsB = Math.Min(lookahead, lenB - j - 1);
                    if (maxInsB > 0)
                    {
                        int idx = targetState.Slice(j + 1, maxInsB).IndexOf(startState[i + 1]);
                        if (idx != -1) insertMatch = j + 1 + idx;
                    }
                }

                if (removeMatch != -1 && (insertMatch == -1 || removeMatch - i <= insertMatch - j))
                {
                    int removeLen = removeMatch - (i + 1);
                    if (diffValueCount + 1 > maxDiffValueCount) goto Fail;
                    changes.Add(new SeqChange(SeqChangeType.Modify, i, 1));
                    if (removeLen > 0)
                    {
                        changes.Add(new SeqChange(SeqChangeType.Remove, i + 1, removeLen));
                    }
                    diffValueCount += 1;
                    i = removeMatch;
                    j += 1;
                }
                else if (insertMatch != -1)
                {
                    int insertLen = insertMatch - (j + 1);
                    if (diffValueCount + 1 + insertLen > maxDiffValueCount) goto Fail;
                    changes.Add(new SeqChange(SeqChangeType.Modify, i, 1));
                    if (insertLen > 0)
                    {
                        changes.Add(new SeqChange(SeqChangeType.Insert, i + 1, insertLen));
                    }
                    diffValueCount += (1 + insertLen);
                    i += 1;
                    j = insertMatch;
                }
                else
                {
                    int startI = i;
                    while (i < lenA && j < lenB && startState[i] != targetState[j]) { i++; j++; }
                    int modifyLen = i - startI;
                    if (diffValueCount + modifyLen > maxDiffValueCount) goto Fail;
                    changes.Add(new SeqChange(SeqChangeType.Modify, startI, modifyLen));
                    diffValueCount += modifyLen;
                }
            }
        }

        // 4. Process trailing elements
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