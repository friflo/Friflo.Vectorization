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
    internal readonly   byte[]  bitmaskData;

    public   override   string  ToString() => $"{width} x {height}";

    internal TuiSixel(int width, int height, byte[] data) {
        this.width  = width;
        this.height = height;
        this.data   = data;
                
        int bandCount = (height + 5) / 6;
        
        // Exact size: 1 byte per pixel (+ optional alignment padding if needed)
        bitmaskData = new byte[width * height];
        
        UpdateFromRgb888(width, height, data, bandCount, bitmaskData, 4);
    }

    private static void UpdateFromRgb888(int width, int height, ReadOnlySpan<byte> src, int bandCount, byte[] bitmaskData, int bytesPerPixel)
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
                bitmaskData[rowOffset + x] = colorIndex;
            }
        }
    }
}