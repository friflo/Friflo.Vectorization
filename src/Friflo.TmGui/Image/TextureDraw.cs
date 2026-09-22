// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Runtime.CompilerServices;
using Friflo.TmGui.TUI;
using System.Numerics;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.Image;


internal class TextureDraw : TmBatch
{
    private readonly TuiSixel sixel;
    
    internal TextureDraw(TmGuiBackend backend, TmTexture texture) : base(backend)
    {
        var tuiTexture = (TuiTexture)texture.native!;
        sixel = tuiTexture.sixel;
    }

    protected internal override void InitBatch()
    {
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Color32ToR3G3B2(Color32 color)
    {
        byte colorIndex = (byte)((color.R & 0xE0) | ((color.G & 0xE0) >> 3) | (color.B >> 6));

        // Reserve index 0 for transparency across the entire engine
        return colorIndex == 0 ? (byte)1 : colorIndex;
    }


    internal void FillRect(Vector2 position, Vector2 size, Color32 color)
    {
        int width   = sixel.width;
        int height  = sixel.height;

        // Convert Vector2 pixel coordinates to integers
        int xStart      = (int)MathF.Round(position.X);
        int yStart      = (int)MathF.Round(position.Y);
        int rectWidth   = (int)MathF.Round(size.X);
        int rectHeight  = (int)MathF.Round(size.Y);

        if (rectWidth <= 0 || rectHeight <= 0) return;

        // Bounds checking and clipping against the Sixel buffer dimensions
        int xEnd = Math.Min(xStart + rectWidth, width);
        int yEnd = Math.Min(yStart + rectHeight, height);

        xStart = Math.Max(xStart, 0);
        yStart = Math.Max(yStart, 0);

        if (xStart >= xEnd || yStart >= yEnd) return;

        // Convert Color32 to 8-bit R3G3B2 byte index using bit shifts
        byte colorIndex = Color32ToR3G3B2(color);

        Span<byte> target = sixel.colorIndexes;
        int fillLength = xEnd - xStart;

        // Fill horizontal spans using SIMD-optimized Span.Fill
        for (int y = yStart; y < yEnd; y++)
        {
            int rowOffset = y * width + xStart;
            target.Slice(rowOffset, fillLength).Fill(colorIndex);
        }
        
        sixel.UpdatePalette(); // TODO  remove hack
    }
}