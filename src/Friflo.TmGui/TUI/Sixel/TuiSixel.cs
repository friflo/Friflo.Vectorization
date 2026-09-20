// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;

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
                
        // int bandCount = (height + 5) / 6;
        
        // Exact size: 1 byte per pixel (+ optional alignment padding if needed)
        
        int count = width * height;
        colorIndexes = new byte[count];
        
        UpdateFromRgb888(width, height, data, colorIndexes, 4);
        
        paletteCount = UpdatePalette(colorIndexes, palette);
    }
    
    private static int UpdatePalette(byte[] colorIndexes, byte[] palette)
    {
        Span<bool> usedColors = stackalloc bool[256];
        foreach (var index in colorIndexes.AsSpan()) {
            usedColors[index] = true;
        }
        int paletteCount = 0;
        for (int n = 0; n < 256; n++) {
            if (!usedColors[n]) continue;
            palette[paletteCount++] = (byte)n;
        }
        return paletteCount;
    }

    private static void UpdateFromRgb888(int width, int height, ReadOnlySpan<byte> src, byte[] colorIndexes, int bytesPerPixel)
    {
        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;
            int srcRowOffset = rowOffset * bytesPerPixel;

            for (int x = 0; x < width; x++)
            {
                int pixelOffset = srcRowOffset + (x * bytesPerPixel);

                byte r = src[pixelOffset];
                byte g = src[pixelOffset + 1];
                byte b = src[pixelOffset + 2];

                // Fast R3G3B2 color quantization (0..255)
                byte colorIndex = (byte)((r & 0xE0) | ((g & 0xE0) >> 3) | (b >> 6));

                // Direct 1-to-1 mapping into the width * height buffer
                colorIndexes[rowOffset + x] = colorIndex;
            }
        }
    }
    
    private static int AppendHeaderToTargetBuffer(Span<byte> target, ReadOnlySpan<byte> palette)
    {
        int writtenBytes = 0;

        // 1. Write SIXEL Header with explicit transparency mode (7;1;1)
        target[writtenBytes++] = 0x1B; // ESC
        target[writtenBytes++] = (byte)'P';
        target[writtenBytes++] = (byte)'7';
        target[writtenBytes++] = (byte)';';
        target[writtenBytes++] = (byte)'1';
        target[writtenBytes++] = (byte)';';
        target[writtenBytes++] = (byte)'1'; // Background Mode / Transparency
        target[writtenBytes++] = (byte)'q';

        // 2. Define R3G3B2 Color Palette (#index;2;r%;g%;b%)
        foreach (var color in palette)
        {
            int r = (color >> 5) & 0x07;
            int g = (color >> 2) & 0x07;
            int b = color & 0x03;

            int rPct = (r * 100) / 7;
            int gPct = (g * 100) / 7;
            int bPct = (b * 100) / 3;

            target[writtenBytes++] = (byte)'#';
            writtenBytes += WriteIntToSpan(color, target.Slice(writtenBytes));
            target[writtenBytes++] = (byte)';';
            target[writtenBytes++] = (byte)'2'; // RGB Percent mode
            target[writtenBytes++] = (byte)';';
            writtenBytes += WriteIntToSpan(rPct, target.Slice(writtenBytes));
            target[writtenBytes++] = (byte)';';
            writtenBytes += WriteIntToSpan(gPct, target.Slice(writtenBytes));
            target[writtenBytes++] = (byte)';';
            writtenBytes += WriteIntToSpan(bPct, target.Slice(writtenBytes));
        }
        return writtenBytes;
    }
    
    internal static int AppendColorIndexesToTargetBuffer(int width, int height, byte[] colorIndexes, Span<byte> target, ReadOnlySpan<byte> palette)
    {
        var writtenBytes = AppendHeaderToTargetBuffer (target, palette);
            
       // Flattened bitmask array: [color * width + x]
        Span<byte> colorBitmasks = stackalloc byte[256 * width];
        Span<bool> usedColors = stackalloc bool[256];

        int bandCount = (height + 5) / 6;

        for (int band = 0; band < bandCount; band++)
        {
            int startY = band * 6;
            int endY = Math.Min(startY + 6, height);

            if (band > 0)
            {
                target[writtenBytes++] = (byte)'-';
            }

            // Clear stack buffers for the current band
            colorBitmasks.Clear();
            usedColors.Clear();

            // Single pass over band pixels: O(width * bandHeight)
            for (int y = startY; y < endY; y++)
            {
                int rowInBand = y - startY;
                int bit = 1 << rowInBand;
                int rowOffset = y * width;

                for (int x = 0; x < width; x++)
                {
                    byte colorIndex = colorIndexes[rowOffset + x];
                    colorBitmasks[colorIndex * width + x] |= (byte)bit;
                    usedColors[colorIndex] = true;
                }
            }

            // Write SIXEL data only for colors present in this band
            for (int color = 0; color < 256; color++)
            {
                if (!usedColors[color]) continue;

                target[writtenBytes++] = (byte)'#';
                writtenBytes += WriteIntToSpan(color, target.Slice(writtenBytes));

                int maskOffset = color * width;
                for (int x = 0; x < width; x++)
                {
                    byte mask = colorBitmasks[maskOffset + x];
                    target[writtenBytes++] = (byte)(63 + mask);
                }

                target[writtenBytes++] = (byte)'$';
            }
        }

        // 3. Write SIXEL Footer: ST (ESC \)
        target[writtenBytes++] = 0x1B; // ESC
        target[writtenBytes++] = (byte)'\\';

        return writtenBytes;
    }

    private static int WriteIntToSpan(int value, Span<byte> destination)
    {
        if (value >= 100)
        {
            destination[0] = (byte)('0' + (value / 100));
            destination[1] = (byte)('0' + ((value / 10) % 10));
            destination[2] = (byte)('0' + (value % 10));
            return 3;
        }
        if (value >= 10)
        {
            destination[0] = (byte)('0' + (value / 10));
            destination[1] = (byte)('0' + (value % 10));
            return 2;
        }
        destination[0] = (byte)('0' + value);
        return 1;
    }
}