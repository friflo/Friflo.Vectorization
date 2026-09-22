// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Runtime.CompilerServices;
using Friflo.TmGui.TUI;
using System.Numerics;

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.Image;


internal class TextureDraw : TmBatch
{
    internal readonly TuiSixel sixel;
    
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
        // Check alpha early for full transparency
        if (color.A < TuiSixel.TransparencyThreshold) {
            return;
        }

        // Apply matrix transform to position and size (assuming 2D translation and scaling)
        Vector2 transformedPos = Vector2.Transform(position, currentTransform);
        
        // Scale dimensions using matrix M11 (X-scale) and M22 (Y-scale)
        Vector2 transformedSize = new Vector2(
            size.X * currentTransform.M11,
            size.Y * currentTransform.M22
        );

        // Convert transformed coordinates to integer space
        int xStart      = (int)MathF.Round(transformedPos.X);
        int yStart      = (int)MathF.Round(transformedPos.Y);
        int rectWidth   = (int)MathF.Round(transformedSize.X);
        int rectHeight  = (int)MathF.Round(transformedSize.Y);

        if (rectWidth <= 0 || rectHeight <= 0) return;

        // Determine scissor region bounds
        int scissorXStart   = (int)MathF.Round(currentScissor.pos.X);
        int scissorYStart   = (int)MathF.Round(currentScissor.pos.Y);
        int scissorXEnd     = (int)MathF.Round(currentScissor.BR.X);
        int scissorYEnd     = (int)MathF.Round(currentScissor.BR.Y);

        // Calculate final intersection between screen buffer, transform, and scissor rect
        int minX = Math.Max(xStart, Math.Max(0, scissorXStart));
        int minY = Math.Max(yStart, Math.Max(0, scissorYStart));
        int maxX = Math.Min(xStart + rectWidth,  Math.Min(sixel.width,  scissorXEnd));
        int maxY = Math.Min(yStart + rectHeight, Math.Min(sixel.height, scissorYEnd));

        // Return if rectangle is completely culled by scissor or buffer bounds
        if (minX >= maxX || minY >= maxY) return;

        // Convert color to R3G3B2 index (reserving index 0 for transparency)
        byte colorIndex = Color32ToR3G3B2(color);

        Span<byte> target = sixel.colorIndexes;
        int bufferWidth = sixel.width;
        int fillLength = maxX - minX;

        // Draw clipped horizontal spans directly into the 1-byte pixel buffer
        for (int y = minY; y < maxY; y++)
        {
            int rowOffset = y * bufferWidth + minX;
            target.Slice(rowOffset, fillLength).Fill(colorIndex);
        }

        sixel.UpdatePalette(); // TODO  remove hack
    }
}