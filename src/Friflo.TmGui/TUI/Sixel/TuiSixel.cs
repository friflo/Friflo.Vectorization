// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

// ReSharper disable SuggestVarOrType_Elsewhere
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
    internal            bool    isDirty;
    
    /// <summary> Linear 1-byte-per-pixel buffer containing R3G3B2 indexed color values. </summary>
    /// <remarks> Total size is exactly <c>width * height</c> bytes. </remarks>
    internal readonly   byte[]  colorIndexes;
    private  readonly   byte[]  palette = new byte[256];
    private             int     paletteCount;
    
    internal ReadOnlySpan<byte> Palette => new ReadOnlySpan<byte>(palette, 0, paletteCount);

    public   override   string  ToString() => $"{width} x {height}";

    internal TuiSixel(int width, int height, byte[] data) {
        this.width  = width;
        this.height = height;
        this.data   = data;
                
        // Exact size: 1 byte per pixel (+ optional alignment padding if needed)
        int count = width * height;
        colorIndexes = new byte[count];
        
        UpdateFromRgb888_SIMD(width, height, data, colorIndexes, 4);
        // SetDebugCorners(colorIndexes, width, height, 0xffffffff);
        
        UpdatePalette();
    }
    
    internal void Clear(Color32 color)
    {
        var fillIndex = color.A < TransparencyThreshold  ? (byte)0 : Color32ToR3G3B2(color);
        colorIndexes.AsSpan().Fill(fillIndex);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Color32ToR3G3B2(Color32 color)
    {
        byte colorIndex = (byte)((color.R & 0xE0) | ((color.G & 0xE0) >> 3) | (color.B >> 6));

        // Reserve index 0 for transparency across the entire engine
        return colorIndex == 0 ? (byte)1 : colorIndex;
    }

#region MyRegion update palette
    internal void UpdatePalette () => paletteCount = UpdatePalette_SIMD(colorIndexes, palette);
    
    private static int UpdatePalette_scalar(ReadOnlySpan<byte> colorIndexes, byte[] palette)
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
    
    private static int UpdatePalette_SIMD(ReadOnlySpan<byte> colorIndexes, Span<byte> palette)
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


#region MyRegion set color indexes from RGB 888

    
    private static void UpdateFromRgb888_scalar(int width, int height, ReadOnlySpan<byte> src, Span<byte> colorIndexes, int bytesPerPixel)
    {
        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;
            int srcRowOffset = rowOffset * bytesPerPixel;

            for (int x = 0; x < width; x++)
            {
                int pixelOffset = srcRowOffset + (x * bytesPerPixel);

                // Check alpha channel for transparency (assuming RGBA format if bytesPerPixel == 4)
                if (bytesPerPixel >= 4 && src[pixelOffset + 3] < TransparencyThreshold)
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
    
    internal const float TransparencyThreshold = 128;
    
    private static void UpdateFromRgb888_SIMD(int width, int height, ReadOnlySpan<byte> src, Span<byte> colorIndexes, int bytesPerPixel)
    {
        int totalPixels = width * height;
        int pixelIdx = 0;

        ref byte srcRef = ref MemoryMarshal.GetReference(src);
        ref byte dstRef = ref MemoryMarshal.GetReference(colorIndexes);
        
        // 256-Bit AVX2 Ultra Fast-Path (Processes 8 pixels per iteration)
        if (bytesPerPixel == 4 && Vector256.IsHardwareAccelerated)
        {
            Vector256<uint> maskRed   = Vector256.Create(0x000000E0u);
            Vector256<uint> maskGreen = Vector256.Create(0x0000E000u);
            Vector256<uint> maskBlue  = Vector256.Create(0x00C00000u);
            Vector256<uint> threshold = Vector256.Create((uint)TransparencyThreshold);

            Vector256<uint> zero = Vector256<uint>.Zero;
            Vector256<uint> one  = Vector256.Create(1u);

            int simdLimit = totalPixels - 8;

            for (; pixelIdx <= simdLimit; pixelIdx += 8)
            {
                // 1. Load 8 RGBA pixels (32 bytes) at once
                Vector256<uint> rgba = Vector256.LoadUnsafe(ref srcRef, (uint)(pixelIdx * 4)).AsUInt32();

                // 2. Isolate & Shift channels
                Vector256<uint> r = rgba & maskRed;
                Vector256<uint> g = (rgba & maskGreen) >> 11;
                Vector256<uint> b = (rgba & maskBlue) >> 22;

                Vector256<uint> q = r | g | b;

                // 3. Remap black (0 -> 1)
                Vector256<uint> isZero = Vector256.Equals(q, zero);
                Vector256<uint> qAdjusted = Vector256.ConditionalSelect(isZero, one, q);

                // 4. Alpha check
                Vector256<uint> alpha = rgba >> 24;
                Vector256<uint> isTransparent = Vector256.LessThan(alpha, threshold);
                Vector256<uint> finalIndices = Vector256.AndNot(qAdjusted, isTransparent);

                // 5. Pack 8x 32-bit lanes down to 8x 8-bit bytes without expensive shuffle
                // Pack uint32 -> uint16 -> uint8
                Vector256<ushort> packed16 = Vector256.Narrow(finalIndices, finalIndices);
                Vector128<byte> packed8 = Vector128.Narrow(packed16.GetLower(), packed16.GetUpper());

                // 6. Write 8 bytes directly to output buffer
                ulong eightIndexBytes = packed8.AsUInt64().GetElement(0);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref dstRef, pixelIdx), eightIndexBytes);
            }
        }

        // 100% Pure Vector Fast-Path for 32-bit RGBA (Processes 4 pixels entirely in SIMD registers)
        if (bytesPerPixel == 4 && Vector128.IsHardwareAccelerated)
        {
            // Masks for 32-bit Little-Endian RGBA layout (0xAABBGGRR)
            Vector128<uint> maskRed   = Vector128.Create(0x000000E0u); // Bits 7..5
            Vector128<uint> maskGreen = Vector128.Create(0x0000E000u); // Bits 15..13
            Vector128<uint> maskBlue  = Vector128.Create(0x00C00000u); // Bits 23..22
            
            Vector128<uint> threshold = Vector128.Create((uint)TransparencyThreshold);

            Vector128<uint> zero = Vector128<uint>.Zero;
            Vector128<uint> one  = Vector128.Create(1u);

            int simdLimit = totalPixels - 4;

            for (; pixelIdx <= simdLimit; pixelIdx += 4)
            {
                // 1. Load 4 RGBA pixels (16 bytes) as 4x 32-bit uints in SIMD register
                Vector128<uint> rgba = Vector128.LoadUnsafe(ref srcRef, (uint)(pixelIdx * 4)).AsUInt32();

                // 2. Isolate & Shift channels directly using 32-bit SIMD lane shifts
                Vector128<uint> r = rgba & maskRed;
                Vector128<uint> g = (rgba & maskGreen) >> 11;
                Vector128<uint> b = (rgba & maskBlue) >> 22;

                // Combine into R3G3B2 index (occupies bits 7..0 of each 32-bit lane)
                Vector128<uint> q = r | g | b;

                // 3. Remap true black (0) to 1 inside SIMD register
                Vector128<uint> isZero = Vector128.Equals(q, zero);
                Vector128<uint> qAdjusted = Vector128.ConditionalSelect(isZero, one, q);

                // 4. Alpha Masking using explicit Vector128.LessThan: Extract alpha (bits 31..24) and check if Alpha < TransparencyThreshold
                Vector128<uint> alpha = rgba >> 24;
                Vector128<uint> isTransparent = Vector128.LessThan(alpha, threshold);

                // Clear palette index to 0 for transparent pixels
                Vector128<uint> finalIndices = Vector128.AndNot(qAdjusted, isTransparent);

                // 5. Pack 4x 32-bit lane results down to 4 contiguous bytes using hardware Narrowing
                Vector128<ushort> packed16 = Vector128.Narrow(finalIndices, finalIndices);
                Vector128<byte> packed8 = Vector128.Narrow(packed16, packed16);

                // 6. Write 4 index bytes to destination in a single 32-bit store operation
                uint fourIndexBytes = packed8.AsUInt32().GetElement(0);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref dstRef, pixelIdx), fourIndexBytes);
            }
        }

        // Scalar fallback for remaining trailing pixels or RGB24
        for (; pixelIdx < totalPixels; pixelIdx++)
        {
            int pixelOffset = pixelIdx * bytesPerPixel;

            byte a = (bytesPerPixel >= 4) ? Unsafe.Add(ref srcRef, pixelOffset + 3) : (byte)255;
            if (a < TransparencyThreshold)
            {
                Unsafe.Add(ref dstRef, pixelIdx) = 0;
                continue;
            }

            byte r = Unsafe.Add(ref srcRef, pixelOffset);
            byte g = Unsafe.Add(ref srcRef, pixelOffset + 1);
            byte b = Unsafe.Add(ref srcRef, pixelOffset + 2);

            byte colorIndex = (byte)((r & 0xE0) | ((g & 0xE0) >> 3) | (b >> 6));
            Unsafe.Add(ref dstRef, pixelIdx) = (colorIndex == 0) ? (byte)1 : colorIndex;
        }
    }
#endregion

    
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