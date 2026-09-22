// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable UnusedMember.Local
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.TUI;

public sealed class TuiSixel
{
    internal readonly   int     width;
    internal readonly   int     height;
    internal readonly   byte[]  data;
    
    // Size: exactly width * height bytes (1/4 of RGBA size)
    internal readonly   byte[]  colorIndexes;
    private  readonly   byte[]  palette = new byte[256];
    private  readonly   int     paletteCount;
    
    internal ReadOnlySpan<byte> Palette => new ReadOnlySpan<byte>(palette, 0, paletteCount);

    public   override   string  ToString() => $"{width} x {height}";

    internal TuiSixel(int width, int height, byte[] data) {
        this.width  = width;
        this.height = height;
        this.data   = data;
                
        // Exact size: 1 byte per pixel (+ optional alignment padding if needed)
        int count = width * height;
        colorIndexes = new byte[count];
        
        UpdateFromRgb888(width, height, data, colorIndexes, 4);
        // SetDebugCorners(colorIndexes, width, height, 0xffffffff);
        
        paletteCount = UpdatePaletteSimd(colorIndexes, palette);
    }

#region MyRegion update palette
    private static int UpdatePalette(ReadOnlySpan<byte> colorIndexes, byte[] palette)
    {
        Span<bool> usedColors = stackalloc bool[256];
        foreach (var index in colorIndexes) {
            // Index 0 is reserved for transparent background
            usedColors[index] = true;
        }
        usedColors[0] = false;
        
        int paletteCount = 0;
        for (int n = 1; n < 256; n++) {
            if (!usedColors[n]) continue;
            palette[paletteCount++] = (byte)n;
        }
        return paletteCount;
    }
    
    private static int UpdatePaletteSimd(ReadOnlySpan<byte> colorIndexes, Span<byte> palette)
    {
        // Use four 64-bit integers to represent a 256-bit bitmask (256 color palette slots).
        // This keeps all color usage state entirely inside CPU registers.
        ulong b0 = 0, b1 = 0, b2 = 0, b3 = 0;

        int i = 0;
        int length = colorIndexes.Length;

        // SIMD fast-path: Process 32 pixel bytes per iteration when SIMD hardware acceleration is available
        if (Vector256.IsHardwareAccelerated)
        {
            for (; i <= length - Vector256<byte>.Count; i += Vector256<byte>.Count)
            {
                // Safely load 32 bytes from the span reference without requiring unsafe pointers
                var vec = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(colorIndexes), (uint)i);

                // Set bits in register bitmasks for each encountered palette index
                for (int v = 0; v < Vector256<byte>.Count; v++)
                {
                    byte index = vec.GetElement(v);
                    // Map index (0..255) to the corresponding 64-bit mask chunk (b0..b3)
                    // Index 0 represents the transparent background color
                    if (index < 64)       b0 |= 1UL << index;           // Colors 0..63 (includes index 0 for transparency)
                    else if (index < 128) b1 |= 1UL << (index - 64);    // Colors 64..127
                    else if (index < 192) b2 |= 1UL << (index - 128);   // Colors 128..191
                    else                  b3 |= 1UL << (index - 192);   // Colors 192..255
                }
            }
        }

        // Process remaining trailing pixels (or non-SIMD fallback)
        for (; i < length; i++)
        {
            byte index = colorIndexes[i];
            if (index < 64)       b0 |= 1UL << index;
            else if (index < 128) b1 |= 1UL << (index - 64);
            else if (index < 192) b2 |= 1UL << (index - 128);
            else                  b3 |= 1UL << (index - 192);
        }

        // Reserve index 0 for transparent background (clear bit 0 in b0)
        b0 &= ~1UL;

        int paletteCount = 0;

        // Extract set bit positions using hardware intrinsic instructions
        paletteCount += ExtractIndices(b0, 0, palette.Slice(paletteCount));
        paletteCount += ExtractIndices(b1, 64, palette.Slice(paletteCount));
        paletteCount += ExtractIndices(b2, 128, palette.Slice(paletteCount));
        paletteCount += ExtractIndices(b3, 192, palette.Slice(paletteCount));

        return paletteCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ExtractIndices(ulong mask, int baseIndex, Span<byte> destination)
    {
        int count = 0;
        while (mask != 0)
        {
            // TrailingZeroCount compiles down to a single hardware instruction (TZCNT/BSF)
            // to directly locate the next set bit without stepping sequentially.
            int bitPos = BitOperations.TrailingZeroCount(mask);
            destination[count++] = (byte)(baseIndex + bitPos);
            
            // Clear the lowest set bit
            mask &= mask - 1;
        }
        return count;
    }
#endregion

    
    private static void UpdateFromRgb888(int width, int height, ReadOnlySpan<byte> src, byte[] colorIndexes, int bytesPerPixel)
    {
        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;
            int srcRowOffset = rowOffset * bytesPerPixel;

            for (int x = 0; x < width; x++)
            {
                int pixelOffset = srcRowOffset + (x * bytesPerPixel);

                // Check alpha channel for transparency (assuming RGBA format if bytesPerPixel == 4)
                if (bytesPerPixel >= 4 && src[pixelOffset + 3] < 128)
                {
                    // Map transparent pixel directly to index 0
                    colorIndexes[rowOffset + x] = 0;
                    continue;
                }
                
                byte r = src[pixelOffset];
                byte g = src[pixelOffset + 1];
                byte b = src[pixelOffset + 2];
                
                // Fast R3G3B2 color quantization (0..255)
                byte colorIndex = (byte)((r & 0xE0) | ((g & 0xE0) >> 3) | (b >> 6));

                // If quantized color lands on index 0 (true black), map to index 1 to reserve 0 for transparency
                if (colorIndex == 0) {
                    colorIndex = 1;
                }
                // Direct 1-to-1 mapping into the width * height buffer
                colorIndexes[rowOffset + x] = colorIndex;
            }
        }
    }
    
    private static void SetDebugCorners(byte[] colorIndexes, int width, int height, Color32 color)
    {
        int r3 = color.R >> 5;
        int g3 = color.G >> 5;
        int b2 = color.B >> 6;

        var colorIndex = (byte)((r3 << 5) | (g3 << 2) | b2);

        // Top-Left
        colorIndexes[0] = colorIndex;
        // Top-Right
        colorIndexes[width - 1] = colorIndex;
        // Bottom-Left
        colorIndexes[(height - 1) * width] = colorIndex;
        // Bottom-Right
        colorIndexes[(height - 1) * width + (width - 1)] = colorIndex;
    }
}