// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ReplaceSliceWithRangeIndexer
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;


public static partial class SequenceDiff
{
    /// <summary>
    /// Extracts modified and inserted items from target into a contiguous diff payload span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static Span<T> AppendDiffValues<T>(
        List<SeqChange> changeList,
        ReadOnlySpan<T> target,
        Span<T>         diffValues) where T : struct
    {
        int targetOffset = 0;
        ReadOnlySpan<SeqChange> changes = CollectionsMarshal.AsSpan(changeList);

        foreach (ref readonly var change in changes)
        {
            if (change.Type is SeqChangeType.Modify or SeqChangeType.Insert)
            {
                int count        = change.Length;
                int sourceOffset = change.Start;

                target.Slice(sourceOffset, count)
                    .CopyTo(diffValues.Slice(targetOffset, count));

                targetOffset += count;
            }
        }

        return diffValues.Slice(0, targetOffset);
    }
    
    
    
   
    /// <summary>
    /// The elements modified or inserted are stored in <see cref="changeList"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void ApplyChanges<T>(
        ReadOnlySpan<T>     startState,
        List<SeqChange>     changeList,
        ReadOnlySpan<T>     diffValues,
        Span<T>             targetState) where T : struct
    {
        int readOffset  = 0;
        int writeOffset = 0;
        int diffOffset  = 0;
         ReadOnlySpan<SeqChange> changes = CollectionsMarshal.AsSpan(changeList);

        foreach (ref readonly var change in changes)
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
                    // Copy new/updated payload from diffItems
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
        }
    }
}