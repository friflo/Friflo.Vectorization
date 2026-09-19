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

    public   override   string  ToString() => $"{width} x {height}";

    internal TuiSixel(int width, int height, byte[] data) {
        this.width  = width;
        this.height = height;
        this.data   = data;
                
        // int bandCount = (height + 5) / 6;
        
        // Exact size: 1 byte per pixel (+ optional alignment padding if needed)
        colorIndexes = new byte[width * height];
        
        UpdateFromRgb888(width, height, data, colorIndexes, 4);
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
    
    internal static int AppendColorIndexesToTargetBuffer(int width, int height, byte[] colorIndexes, Span<byte> target)
    {
        // Temporary 256-entry lookup to track active colors and their bitmasks within a 6-row band
        Span<ushort> colorBitmasks = stackalloc ushort[256];
        int writtenBytes = 0;

        int bandCount = (height + 5) / 6;

        for (int band = 0; band < bandCount; band++)
        {
            int startY = band * 6;
            int endY = Math.Min(startY + 6, height);

            // 1. Flush SIXEL Band Start / DECGNL (Graphics New Line "-") between bands
            if (band > 0)
            {
                target[writtenBytes++] = (byte)'-';
            }

            // 2. Process all 256 possible colors for the current 6-row band
            for (int color = 0; color < 256; color++)
            {
                bool colorUsedInBand = false;

                // Step A: Collect 6-row bitmasks for the current color across all columns (x)
                for (int x = 0; x < width; x++)
                {
                    byte columnBitmask = 0;

                    for (int y = startY; y < endY; y++)
                    {
                        int pixelIndex = y * width + x;

                        if (colorIndexes[pixelIndex] == color)
                        {
                            int rowInBand = y - startY;
                            columnBitmask |= (byte)(1 << rowInBand);
                        }
                    }

                    colorBitmasks[x] = columnBitmask;

                    if (columnBitmask > 0)
                    {
                        colorUsedInBand = true;
                    }
                }

                // Step B: If the color is used in this band, write color introducer and SIXEL characters
                if (colorUsedInBand)
                {
                    // Write SIXEL color selection string: "#<colorIndex>"
                    target[writtenBytes++] = (byte)'#';
                    
                    // Fast ASCII formatting for color index (0..255) without allocations
                    if (color >= 100)
                    {
                        target[writtenBytes++] = (byte)('0' + (color / 100));
                        target[writtenBytes++] = (byte)('0' + ((color / 10) % 10));
                    }
                    else if (color >= 10)
                    {
                        target[writtenBytes++] = (byte)('0' + (color / 10));
                    }
                    target[writtenBytes++] = (byte)('0' + (color % 10));

                    // Step C: Append SIXEL characters ('?' to '~') for each column
                    for (int x = 0; x < width; x++)
                    {
                        byte mask = (byte)colorBitmasks[x];
                        // SIXEL character encoding offset (+63 / ASCII '?')
                        target[writtenBytes++] = (byte)(63 + mask);
                    }

                    // Graphics Carriage Return ('$') to reset cursor position for next color in same band
                    target[writtenBytes++] = (byte)'$';
                }
            }
        }

        return writtenBytes;
    }
}