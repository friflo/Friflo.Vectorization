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
    
    private static int AppendColorIndexesToTargetBuffer(int width, int height, byte[] colorIndexes, Span<byte> target)
    {
        Span<ushort> colorBitmasks = stackalloc ushort[256];
        int writtenBytes = 0;

        // 1. Write SIXEL Header: DCS (ESC P 7 ; 1 ; q)
        // "7;1" specifies aspect ratio and grid unit
        target[writtenBytes++] = 0x1B; // ESC
        target[writtenBytes++] = (byte)'P';
        target[writtenBytes++] = (byte)'7';
        target[writtenBytes++] = (byte)';';
        target[writtenBytes++] = (byte)'1';
        target[writtenBytes++] = (byte)';';
        target[writtenBytes++] = (byte)'q';

        int bandCount = (height + 5) / 6;

        for (int band = 0; band < bandCount; band++)
        {
            int startY = band * 6;
            int endY = Math.Min(startY + 6, height);

            if (band > 0)
            {
                target[writtenBytes++] = (byte)'-';
            }

            for (int color = 0; color < 256; color++)
            {
                bool colorUsedInBand = false;

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

                if (colorUsedInBand)
                {
                    target[writtenBytes++] = (byte)'#';
                    
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

                    for (int x = 0; x < width; x++)
                    {
                        byte mask = (byte)colorBitmasks[x];
                        target[writtenBytes++] = (byte)(63 + mask);
                    }

                    target[writtenBytes++] = (byte)'$';
                }
            }
        }

        // 2. Write SIXEL Footer: ST (ESC \)
        target[writtenBytes++] = 0x1B; // ESC
        target[writtenBytes++] = (byte)'\\';

        return writtenBytes;
    }
}