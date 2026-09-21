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
                
        // Exact size: 1 byte per pixel (+ optional alignment padding if needed)
        int count = width * height;
        colorIndexes = new byte[count];
        
        UpdateFromRgb888(width, height, data, colorIndexes, 4);
        SetDebugCorners(colorIndexes, width, height, 0xffffffff);
        
        paletteCount = UpdatePalette(colorIndexes, palette);
    }
    
    private static int UpdatePalette(byte[] colorIndexes, byte[] palette)
    {
        Span<bool> usedColors = stackalloc bool[256];
        foreach (var index in colorIndexes.AsSpan()) {
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