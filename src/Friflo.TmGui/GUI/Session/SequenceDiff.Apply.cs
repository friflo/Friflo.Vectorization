// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ReplaceSliceWithRangeIndexer
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;


public static partial class SequenceDiff
{
    /// <summary>
    /// Extracts modified and inserted items from target into a contiguous diff payload span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static Span<T> FillDiffValues<T>(
        List<SeqChange> changeList,
        ReadOnlySpan<T> target,
        ref T[]         diffValueBuffer,
        int             diffValueCount) where T : unmanaged
    {
        int diffOffset   = 0;
        int targetOffset = 0; // Tracks cumulative (inserts - removes) count shift

        ReadOnlySpan<SeqChange> changes = CollectionsMarshal.AsSpan(changeList);
        var diffValues = GetSpanOf(ref diffValueBuffer, diffValueCount);

        foreach (ref readonly var change in changes)
        {
            int count = change.Length;

            switch (change.Type)
            {
                case SeqChangeType.Modify:
                case SeqChangeType.Insert:
                    // Target position corresponds to change.Start plus the current index shift
                    int sourceIndex = change.Start + targetOffset;
                    target.Slice(sourceIndex, count).CopyTo(diffValues.Slice(diffOffset, count));
                    
                    diffOffset += count;
                    if (change.Type == SeqChangeType.Insert)
                    {
                        targetOffset += count;
                    }
                    break;

                case SeqChangeType.Remove:
                    targetOffset -= count;
                    break;
            }
        }

        Debug.Assert(diffValues.Length == diffOffset);
        return diffValues;
    }
    
    
    
   
    /// <summary>
    /// The elements modified or inserted are stored in <paramref name="diffValues"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static Span<T> ApplyChanges<T>(
        ReadOnlySpan<T>     startState,
        List<SeqChange>     changeList,
        ReadOnlySpan<T>     diffValues,
        ref T[]             targetBuffer,
        int                 targetLength) where T : unmanaged
    {
        int readOffset  = 0;
        int writeOffset = 0;
        int diffOffset  = 0;
        ReadOnlySpan<SeqChange> changes = CollectionsMarshal.AsSpan(changeList);
        Span<T> targetState = GetSpanOf(ref targetBuffer, targetLength);

        foreach (var change in changes)
        {
            // 1. Copy unmodified items leading up to this change
            int unmodifiedCount = change.Start - readOffset;
            if (unmodifiedCount > 0)
            {
                startState.Slice(readOffset, unmodifiedCount)
                    .CopyTo(targetState.Slice(writeOffset, unmodifiedCount));

                readOffset  += unmodifiedCount;
                writeOffset += unmodifiedCount;
            }

            switch (change.Type)
            {
                case SeqChangeType.Modify:
                case SeqChangeType.Insert:
                    // Copy new/updated payload from diffValues
                    diffValues.Slice(diffOffset, change.Length)
                        .CopyTo(targetState.Slice(writeOffset, change.Length));

                    diffOffset  += change.Length;
                    writeOffset += change.Length;

                    if (change.Type == SeqChangeType.Modify)
                    {
                        readOffset += change.Length;
                    }
                    break;

                case SeqChangeType.Remove:
                    // Skip items in startState (don't copy to targetState)
                    readOffset += change.Length;
                    break;
            }
        }

        // Copy remaining tail elements if any
        int remainingCount = startState.Length - readOffset;
        if (remainingCount > 0)
        {
            startState.Slice(readOffset, remainingCount)
                .CopyTo(targetState.Slice(writeOffset, remainingCount));
            writeOffset += remainingCount;
        }
        Debug.Assert(targetState.Length == writeOffset);
        return targetState;
    }
    
    internal static Span<T> GetSpanOf<T>(ref T[] array, int length) where T : unmanaged
    {
        if (array.Length < length) {
            array = new T[Math.Max(length, 2 * array.Length)];
        }
        return array.AsSpan(0, length);
    }
}