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
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static Span<byte> AppendDiffItems<T>(
        List<SeqChange>     changeList,
        ReadOnlySpan<T>     target,
        Span<byte>          targetValues) where T : struct
    {
        ReadOnlySpan<byte> sourceBytes = MemoryMarshal.AsBytes(target);
        int elementSize = Unsafe.SizeOf<T>();
        int targetOffset = 0;
        
        ReadOnlySpan<SeqChange> changes = CollectionsMarshal.AsSpan(changeList);

        foreach (var change in changes)
        {
            if (change.Type is SeqChangeType.Modify or SeqChangeType.Insert)
            {
                int copyBytes       = change.Length * elementSize;
                int sourceOffset    = change.Start  * elementSize;

                sourceBytes.Slice(sourceOffset, copyBytes)
                    .CopyTo(targetValues.Slice(targetOffset, copyBytes));

                targetOffset += copyBytes;
            }
        }
        return targetValues.Slice(0, targetOffset);
    }
    
    
    
    /// <summary>
    /// The elements modified or inserted are stored in <see cref="changes"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void ApplyChanges<T>(
        ReadOnlySpan<T>         startState,
        ReadOnlySpan<SeqChange> changes,
        ReadOnlySpan<T>         diffItems,
        Span<T>                 targetState) where T : struct
    {
        ReadOnlySpan<byte>  startBytes  = MemoryMarshal.AsBytes(startState);
        ReadOnlySpan<byte>  diffBytes   = MemoryMarshal.AsBytes(diffItems);
        Span<byte>          targetBytes = MemoryMarshal.AsBytes(targetState);

        ApplyChanges(startBytes, changes, diffBytes, targetBytes, Unsafe.SizeOf<T>());
    }
    
    /// <summary>
    /// The elements modified or inserted are stored in <see cref="changes"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ApplyChanges(
        ReadOnlySpan<byte>      startState,
        ReadOnlySpan<SeqChange> changes,
        ReadOnlySpan<byte>      diffItems,
        Span<byte>              targetState,
        int                     elementSize)
    {
        int srcElemIndex = 0;
        int dstElemIndex = 0;
        int diffElemIndex = 0;

        foreach (var change in changes)
        {
            int changeStart = change.Start;

            // 1. Copy unchanged elements leading up to this change
            int unchangedCount = changeStart - srcElemIndex;
            if (unchangedCount > 0)
            {
                int copyBytes = unchangedCount * elementSize;
                startState.Slice(srcElemIndex * elementSize, copyBytes)
                    .CopyTo(targetState.Slice(dstElemIndex * elementSize, copyBytes));

                srcElemIndex += unchangedCount;
                dstElemIndex += unchangedCount;
            }

            int changeLen = change.Length;
            int changeBytes = changeLen * elementSize;

            // 2. Process change type
            switch (change.Type)
            {
                case SeqChangeType.Modify:
                    // Copy updated elements from diffItems to targetState
                    diffItems.Slice(diffElemIndex * elementSize, changeBytes)
                        .CopyTo(targetState.Slice(dstElemIndex * elementSize, changeBytes));

                    srcElemIndex += changeLen;
                    dstElemIndex += changeLen;
                    diffElemIndex += changeLen;
                    break;

                case SeqChangeType.Insert:
                    // Copy inserted elements from diffItems to targetState
                    diffItems.Slice(diffElemIndex * elementSize, changeBytes)
                        .CopyTo(targetState.Slice(dstElemIndex * elementSize, changeBytes));

                    dstElemIndex += changeLen;
                    diffElemIndex += changeLen;
                    break;

                case SeqChangeType.Remove:
                    // Skip removed elements in startState
                    srcElemIndex += changeLen;
                    break;
            }
        }

        // 3. Copy remaining trailing unchanged elements
        int remainingSrcBytes = startState.Length - (srcElemIndex * elementSize);
        if (remainingSrcBytes > 0)
        {
            startState.Slice(srcElemIndex * elementSize, remainingSrcBytes)
                      .CopyTo(targetState.Slice(dstElemIndex * elementSize, remainingSrcBytes));
        }
    }
}