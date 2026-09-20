// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.TUI;


public sealed class SixelDrawer
{
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
    
    private int     clipCellsWidth;
    private int     clipCellsHeight;
    private byte[]  clipCellsBuffer = [];

    public void SetClipCells(Span<TuiColorCell> cells, int width, int height)
    {
        clipCellsWidth  = width;
        clipCellsHeight = height;
        
        if (clipCellsBuffer.Length < cells.Length) {
            clipCellsBuffer = new byte[cells.Length];
        }
        var clipCells = clipCellsBuffer.AsSpan(0, cells.Length);
        
        for (int n = 0; n < clipCells.Length; n++) {
            clipCells[n] = cells[n].sixelId; 
        }
    }
    
    private byte[] colorBitmasksBuffer = [];
    
    internal int AppendSixelToTargetBuffer(TuiSixel sixel, Span<byte> target)
    {
        var width           = sixel.width;
        var height          = sixel.height;
        var colorIndexes    = sixel.colorIndexes;
        var palette         = sixel.Palette;
        
        var writtenBytes = AppendHeaderToTargetBuffer(target, palette);
            
        // Flattened bitmask array: [color * width + x]
        var colorBitmasksLength = 256 * width;
        if (colorBitmasksBuffer.Length < colorBitmasksLength) {
            colorBitmasksBuffer = new byte[colorBitmasksLength];
        }
        var colorBitmasks = colorBitmasksBuffer.AsSpan(0, colorBitmasksLength);

        Span<bool> usedColors = stackalloc bool[256];
        Span<byte> activeColors = stackalloc byte[256];

        int bandCount = (height + 5) / 6;

        for (int band = 0; band < bandCount; band++)
        {
            int startY = band * 6;
            int endY = Math.Min(startY + 6, height);

            if (band > 0)
            {
                target[writtenBytes++] = (byte)'-';
            }

            usedColors.Clear();
            int activeColorCount = 0;

            // Single pass over band pixels: O(width * bandHeight)
            for (int y = startY; y < endY; y++)
            {
                int rowInBand = y - startY;
                int bit = 1 << rowInBand;
                int rowOffset = y * width;

                for (int x = 0; x < width; x++)
                {
                    byte colorIndex = colorIndexes[rowOffset + x];

                    // Skip processing transparent pixels (index 0)
                    if (colorIndex == 0) {
                        continue;
                    }

                    // Clear mask row only on first access in this band
                    if (!usedColors[colorIndex]) {
                        usedColors[colorIndex] = true;
                        activeColors[activeColorCount++] = colorIndex;
                        colorBitmasks.Slice(colorIndex * width, width).Clear();
                    }

                    colorBitmasks[colorIndex * width + x] |= (byte)bit;
                }
            }

            // Write SIXEL data only for active colors present in this band
            for (int i = 0; i < activeColorCount; i++)
            {
                byte color = activeColors[i];

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