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
        int                 maxOperations,
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

            if (changes.Count >= maxOperations) goto Fail;

            int valA = startState[i], valB = targetState[j];
            int matchTarget = -1, matchStart = -1;

            // 2. Bound lookahead scan (max 8 elements)
            int maxB = Math.Min(8, lenB - j);
            for (int offset = 1; offset < maxB; offset++)
                if (valA == targetState[j + offset]) { matchTarget = offset; break; }

            if (matchTarget == -1) {
                int maxA = Math.Min(8, lenA - i);
                for (int offset = 1; offset < maxA; offset++)
                    if (startState[i + offset] == valB) { matchStart = offset; break; }
            }

            // 3. Emit matching SeqChange operation
            if (matchTarget > 0)
            {
                changes.Add(new SeqChange(SeqChangeType.Insert, i, matchTarget));
                diffValueCount += matchTarget;
                j += matchTarget;
            }
            else if (matchStart > 0)
            {
                changes.Add(new SeqChange(SeqChangeType.Remove, i, matchStart));
                i += matchStart;
            }
            else
            {
                int startI = i;
                while (i < lenA && j < lenB && startState[i] != targetState[j]) { i++; j++; }
                int modifyLen = i - startI;
                changes.Add(new SeqChange(SeqChangeType.Modify, startI, modifyLen));
                diffValueCount += modifyLen;
            }
        }

        // 4. Process trailing elements
        if (i < lenA) changes.Add(new SeqChange(SeqChangeType.Remove, i, lenA - i));

        if (j < lenB)
        {
            int insertLen = lenB - j;
            changes.Add(new SeqChange(SeqChangeType.Insert, i, insertLen));
            diffValueCount += insertLen;
        }

        if (changes.Count <= maxOperations) return true;

    Fail:
        changes.Clear();
        diffValueCount = -1;
        return false;
    }
}