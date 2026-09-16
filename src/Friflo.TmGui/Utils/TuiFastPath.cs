// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

// ReSharper disable ReplaceSliceWithRangeIndexer
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


internal static class TuiFastPath
{
    /// <summary>
    /// Checks via SIMD whether the text is a single-line pure ASCII string (&lt; 128, no '\n').
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOneLineAscii(ReadOnlySpan<char> text)
    {
        int length = text.Length;
        int i = 0;

        if (Vector128.IsHardwareAccelerated && length >= Vector128<ushort>.Count)
        {
            var asciiMask    = Vector128.Create((ushort)0xFF80);
            var newline      = Vector128.Create((ushort)'\n');
            int vectorSize   = Vector128<ushort>.Count; // 8 chars per Vector128<ushort>

            while (i <= length - vectorSize)
            {
                // Safely read a Vector128<ushort> chunk from the span
                var v = MemoryMarshal.Read<Vector128<ushort>>(MemoryMarshal.AsBytes(text.Slice(i)));

                // 1. Check if any character is >= 128
                if (Vector128.BitwiseAnd(v, asciiMask) != Vector128<ushort>.Zero)
                {
                    return false;
                }

                // 2. Check if any character is '\n'
                if (Vector128.Equals(v, newline) != Vector128<ushort>.Zero)
                {
                    return false;
                }

                i += vectorSize;
            }
        }

        // Handle remaining characters sequentially
        for (; i < length; i++)
        {
            char c = text[i];
            if (c >= 128 || c == '\n')
            {
                return false;
            }
        }

        return true;
    }
}