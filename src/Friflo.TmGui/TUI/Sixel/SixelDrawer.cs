// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// #define DEBUG_CLIPPING

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

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
        foreach (byte color in palette)
        {
            int rPct, gPct, bPct;

            if (color == TuiSixel.SubstituteBack) {
                // Force SubstituteBack (0x20) to represent pure 0% RGB black in the Sixel header
                rPct = 0;
                gPct = 0;
                bPct = 0;
            } else {
                int r = (color >> 5) & 0x07;
                int g = (color >> 2) & 0x07;
                int b = color & 0x03;

                rPct = (r * 100) / 7;
                gPct = (g * 100) / 7;
                bPct = (b * 100) / 3;
            }
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
    
    private int     cellsWidth;
    private int     cellsHeight;
    /// <summary>
    /// Pixels must be drawn only if they are inside a <see cref="clipCellsBuffer"/> having the same sixelId.<br/>
    /// The dimension of clipCellsBuffer is in terminal cells: <see cref="cellsWidth"/> x <see cref="cellsHeight"/>.
    /// </summary>
    private byte[]  clipCellsBuffer = [];

    public void SetClipCells(Span<TuiColorCell> cells, int width, int height)
    {
        cellsWidth  = width;
        cellsHeight = height;
        
        if (clipCellsBuffer.Length < cells.Length) {
            clipCellsBuffer = new byte[cells.Length];
        }
        var clipCells = clipCellsBuffer.AsSpan(0, cells.Length);
        
        for (int n = 0; n < clipCells.Length; n++) {
            clipCells[n] = cells[n].sixelId; 
        }
    }
    
    private byte[] colorBitmasksBuffer = [];
    
    // required to prevent terminal scrolling if bottom sixel band draws into terminal bottom border.
    private static int ClipHeightToBottomSixelBand(int canvasHeight, int imagePosY)
    {
        return canvasHeight - 5; // Ensures consistent distance of 5 pixel from terminal bottom border.
        // Note: Alternative clipping code reduces clipping height. But now clip height alternates depending on bottomDist.
        // var bottomDist = canvasHeight - imagePosY;
        // return imagePosY + (bottomDist / 6) * 6;
    }
    
    internal int AppendSixelToTargetBuffer(
        DrawSixel   drawSixel,
        TuiBatch    tuiBatch,
        Span<byte>  target,
        Vector2     cellPixelSize)
    {
        var sixel        = drawSixel.sixel;
        var targetSize   = drawSixel.size * cellPixelSize / new Vector2(tuiBatch.CharWidth, tuiBatch.LineHeight);
        var width        = sixel.width;
        var height       = sixel.height;
        var colorIndexes = sixel.colorIndexes;
        if (sixel.isDirty) {
            sixel.UpdatePalette();
            sixel.isDirty = false;
            sixel.version++;
        }

        // Subsequent units are in sixel pixels: imagePos, origin & canvas
        var imagePosX = (int)(TuiBatch.FastFloor(drawSixel.pos.X / tuiBatch.CharWidth)  * cellPixelSize.X);
        var imagePosY = (int)(TuiBatch.FastFloor(drawSixel.pos.Y / tuiBatch.LineHeight) * cellPixelSize.Y);
        
        int cursorX = Math.Max((int)(imagePosX / cellPixelSize.X), 0);
        int cursorY = Math.Max((int)(imagePosY / cellPixelSize.Y), 0);
        
        if ((uint)cursorX >= (uint)cellsWidth || (uint)cursorY >= (uint)cellsHeight) {
            return 0;
        }
        var originX = TuiBatch.FastFloor(cursorX * cellPixelSize.X) - imagePosX;
        var originY = TuiBatch.FastFloor(cursorY * cellPixelSize.Y) - imagePosY;

        // Unit of canvasWidth / canvasHeight are sixel pixels
        var canvasWidth  = (int)(cellsWidth  * cellPixelSize.X);
        var canvasHeight = (int)(cellsHeight * cellPixelSize.Y);
        //
        canvasHeight = ClipHeightToBottomSixelBand(canvasHeight, imagePosY);

        // Scaled pixel bounds based on targetSize. Clamp to at least 1 to prevent DivideByZeroException 
        // in fixed-point scale calculations (scaleX16/scaleY16) when target size is near zero.
        int scaledWidth  = (int)Math.Max(1, targetSize.X);
        int scaledHeight = (int)Math.Max(1, targetSize.Y);

        // Calculate raw source boundaries mapped to target rendering area
        int startSrcX = Math.Max(0, originX);
        int startSrcY = Math.Max(0, originY);
        
        int endSrcX   = Math.Min(scaledWidth,  canvasWidth  - imagePosX);
        int endSrcY   = Math.Min(scaledHeight, canvasHeight - imagePosY);

        int renderWidth  = endSrcX - startSrcX;
        int renderHeight = endSrcY - startSrcY;

        if (renderWidth <= 0 || renderHeight <= 0) {
            // Nothing to draw
            return 0;
        }

        int writtenBytes = 0;

        // Set Cursor Position: ESC [ {row} ; {col} H
        target[writtenBytes++] = 0x1B;
        target[writtenBytes++] = (byte)'[';
        writtenBytes += WriteIntToSpan(cursorY + 1, target.Slice(writtenBytes));
        target[writtenBytes++] = (byte)';';
        writtenBytes += WriteIntToSpan(cursorX + 1, target.Slice(writtenBytes));
        target[writtenBytes++] = (byte)'H';

        writtenBytes += AppendHeaderToTargetBuffer(target.Slice(writtenBytes), sixel.Palette);

        writtenBytes += RasterizeBands(
            colorIndexes,
            width,
            height,
            startSrcX,
            startSrcY,
            endSrcX,
            endSrcY,
            renderWidth,
            renderHeight,
            scaledWidth,
            scaledHeight,
            imagePosX,
            imagePosY,
            cellPixelSize,
            drawSixel.sixelId,
            target.Slice(writtenBytes));

        // Write SIXEL Footer: ST (ESC \)
        target[writtenBytes++] = 0x1B; // ESC
        target[writtenBytes++] = (byte)'\\';

        return writtenBytes;
    }

    // Samples the source image using Nearest-Neighbor to preserve performance and crisp rendering
    private int RasterizeBands(
        ReadOnlySpan<byte>  colorIndexes,
        int                 srcWidth,
        int                 srcHeight,
        int                 startSrcX,
        int                 startSrcY,
        int                 endSrcX,
        int                 endSrcY,
        int                 renderWidth,
        int                 renderHeight,
        int                 scaledWidth,
        int                 scaledHeight,
        int                 imagePosX,
        int                 imagePosY,
        Vector2             cellPixelSize,
        byte                targetSixelId,
        Span<byte>          target)
    {
        // Flattened bitmask array: [color * renderWidth + x]
        var colorBitmasksLength = 256 * renderWidth;
        if (colorBitmasksBuffer.Length < colorBitmasksLength) {
            colorBitmasksBuffer = new byte[colorBitmasksLength];
        }
        var clipCells       = clipCellsBuffer;
        var colorBitmasks   = colorBitmasksBuffer.AsSpan(0, colorBitmasksLength);
        HashSetClear(debugSkipped);
        HashSetClear(debugDrawn);

        Span<bool> usedColors   = stackalloc bool[256];
        Span<byte> activeColors = stackalloc byte[256];

        int writtenBytes = 0;
        int bandCount = (renderHeight + 5) / 6;

        float invCellWidthPx  = 1.0f / cellPixelSize.X;
        float invCellHeightPx = 1.0f / cellPixelSize.Y;

        // Fixed-point 16.16 scale factors for Nearest-Neighbor sampling from target to source
        int scaleX16 = (int)(((long)srcWidth  << 16) / scaledWidth);
        int scaleY16 = (int)(((long)srcHeight << 16) / scaledHeight);

        for (int band = 0; band < bandCount; band++)
        {
            int startY = startSrcY + (band * 6);
            int endY = Math.Min(startY + 6, endSrcY);

            if (band > 0) {
                target[writtenBytes++] = (byte)'-';
            }

            usedColors.Clear();
            int activeColorCount = 0;

            // Single pass over band pixels: O(renderWidth * bandHeight)
            for (int y = startY; y < endY; y++)
            {
                // Bit position inside the 6-pixel SIXEL band
                int rowInBand = (y - startSrcY) % 6;
                int bit = 1 << rowInBand;

                int cellY = (int)((imagePosY + y) * invCellHeightPx);
                int cellRowOffset = cellY * cellsWidth;

                // Nearest-neighbor Y sampling in source texture
                int srcY = (int)(((long)y * scaleY16) >> 16);
                if (srcY >= srcHeight) srcY = srcHeight - 1;
                int srcRowOffset = srcY * srcWidth;

                for (int x = startSrcX; x < endSrcX; x++)
                {
                    // Nearest-neighbor X sampling in source texture
                    int srcX = (int)(((long)x * scaleX16) >> 16);
                    if (srcX >= srcWidth) srcX = srcWidth - 1;

                    byte colorIndex = colorIndexes[srcRowOffset + srcX];

                    // Skip processing transparent pixels (index 0)
                    if (colorIndex == 0) {
                        continue;
                    }

                    // Check clip cells buffer for current pixel
                    int cellX = (int)((imagePosX + x) * invCellWidthPx);
                    var clipIndex = (uint)(cellRowOffset + cellX);
                    
                    var skip = clipIndex >= clipCells.Length || clipCells[clipIndex] != targetSixelId;
                    if (skip) {
                        HashSetAdd(debugSkipped, clipIndex);
                        continue;
                    }
                    HashSetAdd(debugDrawn, clipIndex);

                    // Local X coordinate within the rendered target area
                    int localX = x - startSrcX;

                    // Clear mask row only on first access in this band
                    if (!usedColors[colorIndex]) {
                        usedColors[colorIndex] = true;
                        activeColors[activeColorCount++] = colorIndex;
                        colorBitmasks.Slice(colorIndex * renderWidth, renderWidth).Clear();
                    }

                    colorBitmasks[colorIndex * renderWidth + localX] |= (byte)bit;
                }
            }

            // Write SIXEL data only for active colors present in this band
            for (int i = 0; i < activeColorCount; i++)
            {
                byte color = activeColors[i];

                int maskOffset = color * renderWidth;
                var bandMask = colorBitmasks.Slice(maskOffset, renderWidth);

                // Find min/max bounds to trim empty trailing space
                int minX = 0;
                while (minX < renderWidth && bandMask[minX] == 0) {
                    minX++;
                }
                int maxX = renderWidth - 1;
                while (maxX >= minX && bandMask[maxX] == 0) {
                    maxX--;
                }
                // Skip if no active bits remain in this band
                if (minX > maxX) {
                    continue;
                }
                target[writtenBytes++] = (byte)'#';
                writtenBytes += WriteIntToSpan(color, target.Slice(writtenBytes));

                // RLE encode the active window [0..maxX]
                int x = 0;
                while (x <= maxX)
                {
                    byte mask = bandMask[x];
                    int runLength = 1;

                    while (x + runLength <= maxX && bandMask[x + runLength] == mask) {
                        runLength++;
                    }

                    if (runLength > 3) {
                        // Emit SIXEL RLE sequence: !<count><character>
                        target[writtenBytes++] = (byte)'!';
                        writtenBytes += WriteIntToSpan(runLength, target.Slice(writtenBytes));
                        target[writtenBytes++] = (byte)(63 + mask);
                    } else {
                        for (int r = 0; r < runLength; r++) {
                            target[writtenBytes++] = (byte)(63 + mask);
                        }
                    }
                    x += runLength;
                }
                target[writtenBytes++] = (byte)'$';
            }
        }

        // Trim trailing SIXEL graphic newlines ('-') to save I/O and prevent unwanted line feeds
        while (writtenBytes > 0 && target[writtenBytes - 1] == (byte)'-') { 
            writtenBytes--; 
        }

#if DEBUG_CLIPPING
        Debug.WriteLine($"skipped: {debugSkipped.Count}  drawn: {debugDrawn.Count}  writtenBytes: {writtenBytes}");
        // Debug.WriteLine(new string(Encoding.UTF8.GetChars(target.Slice(0, writtenBytes).ToArray())));
#endif
        return writtenBytes;
    }
    
    private readonly HashSet<uint> debugSkipped = [];
    private readonly HashSet<uint> debugDrawn   = [];
    [Conditional("DEBUG_CLIPPING")] private static void HashSetClear(HashSet<uint> hashSet)                 => hashSet.Clear();
    [Conditional("DEBUG_CLIPPING")] private static void HashSetAdd  (HashSet<uint> hashSet, uint cellIndex) => hashSet.Add(cellIndex); 

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