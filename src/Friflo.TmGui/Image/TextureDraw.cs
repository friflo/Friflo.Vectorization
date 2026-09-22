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
    
    
    internal void FillTriangle(Vector2 v0, Vector2 v1, Vector2 v2, Color32 color)
    {
        // Check alpha early for full transparency
        if (color.A < TuiSixel.TransparencyThreshold) return;

        // Apply matrix transformation to all 3 vertices
        Vector2 p0 = Vector2.Transform(v0, currentTransform);
        Vector2 p1 = Vector2.Transform(v1, currentTransform);
        Vector2 p2 = Vector2.Transform(v2, currentTransform);

        // Sort vertices by Y coordinate (p0.Y <= p1.Y <= p2.Y)
        if (p0.Y > p1.Y) (p0, p1) = (p1, p0);
        if (p0.Y > p2.Y) (p0, p2) = (p2, p0);
        if (p1.Y > p2.Y) (p1, p2) = (p2, p1);

        // Calculate Y bounds
        int yStart = (int)MathF.Round(p0.Y);
        int yMid   = (int)MathF.Round(p1.Y);
        int yEnd   = (int)MathF.Round(p2.Y);

        if (yStart == yEnd) return; // Degenerate zero-height triangle

        // Calculate Scissor & Buffer intersection bounds
        int scissorYStart = (int)MathF.Round(currentScissor.pos.Y);
        int scissorYEnd   = (int)MathF.Round(currentScissor.BR.Y);
        int clipYMin      = Math.Max(0, scissorYStart);
        int clipYMax      = Math.Min(sixel.height, scissorYEnd);

        int scissorXStart = (int)MathF.Round(currentScissor.pos.X);
        int scissorXEnd   = (int)MathF.Round(currentScissor.BR.X);
        int clipXMin      = Math.Max(0, scissorXStart);
        int clipXMax      = Math.Min(sixel.width, scissorXEnd);

        // Early out if completely culled vertically
        if (yEnd <= clipYMin || yStart >= clipYMax) return;

        byte colorIndex = Color32ToR3G3B2(color);
        Span<byte> target = sixel.colorIndexes;
        int bufferWidth = sixel.width;

        float totalHeight = p2.Y - p0.Y;

        // --- Top Half (from p0.Y to p1.Y) ---
        int topEnd = Math.Min(yMid, clipYMax);
        for (int y = yStart; y < topEnd; y++)
        {
            if (y < clipYMin) continue;

            float currentY = y + 0.5f; // Center sampling
            float segmentHeight = p1.Y - p0.Y;
            if (segmentHeight <= 0) break;

            // Interpolate X coordinates along edges
            float alpha = (currentY - p0.Y) / totalHeight;
            float beta  = (currentY - p0.Y) / segmentHeight;

            float xA = p0.X + (p2.X - p0.X) * alpha;
            float xB = p0.X + (p1.X - p0.X) * beta;

            DrawScanline(target, bufferWidth, y, xA, xB, clipXMin, clipXMax, colorIndex);
        }

        // --- Bottom Half (from p1.Y to p2.Y) ---
        int bottomStart = Math.Max(yMid, yStart);
        for (int y = bottomStart; y < yEnd; y++)
        {
            if (y >= clipYMax) break;
            if (y < clipYMin) continue;

            float currentY = y + 0.5f; // Center sampling
            float segmentHeight = p2.Y - p1.Y;
            if (segmentHeight <= 0) break;

            // Interpolate X coordinates along edges
            float alpha = (currentY - p0.Y) / totalHeight;
            float beta  = (currentY - p1.Y) / segmentHeight;

            float xA = p0.X + (p2.X - p0.X) * alpha;
            float xB = p1.X + (p2.X - p1.X) * beta;

            DrawScanline(target, bufferWidth, y, xA, xB, clipXMin, clipXMax, colorIndex);
        }
        sixel.UpdatePalette(); // TODO  remove hack
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DrawScanline(Span<byte> target, int bufferWidth, int y, float xA, float xB, int clipXMin, int clipXMax, byte colorIndex)
    {
        if (xA > xB) (xA, xB) = (xB, xA);

        int xStart = Math.Max((int)MathF.Round(xA), clipXMin);
        int xEnd   = Math.Min((int)MathF.Round(xB), clipXMax);

        int fillLength = xEnd - xStart;
        if (fillLength <= 0) return;

        int rowOffset = y * bufferWidth + xStart;
        target.Slice(rowOffset, fillLength).Fill(colorIndex);
    }
    
    internal void FillRectGradientVertical(Vector2 position, Vector2 size, Color32 topColor, Color32 bottomColor)
    {
        // Apply matrix transformation to position and size
        Vector2 transformedPos = Vector2.Transform(position, currentTransform);
        Vector2 transformedSize = new Vector2(
            size.X * currentTransform.M11,
            size.Y * currentTransform.M22
        );

        int xStart     = (int)MathF.Round(transformedPos.X);
        int yStart     = (int)MathF.Round(transformedPos.Y);
        int rectWidth  = (int)MathF.Round(transformedSize.X);
        int rectHeight = (int)MathF.Round(transformedSize.Y);

        if (rectWidth <= 0 || rectHeight <= 0) return;

        // Determine scissor region bounds
        int scissorXStart = (int)MathF.Round(currentScissor.pos.X);
        int scissorYStart = (int)MathF.Round(currentScissor.pos.Y);
        int scissorXEnd   = (int)MathF.Round(currentScissor.BR.X);
        int scissorYEnd   = (int)MathF.Round(currentScissor.BR.Y);

        // Calculate final intersection bounds
        int minX = Math.Max(xStart, Math.Max(0, scissorXStart));
        int minY = Math.Max(yStart, Math.Max(0, scissorYStart));
        int maxX = Math.Min(xStart + rectWidth,  Math.Min(sixel.width,  scissorXEnd));
        int maxY = Math.Min(yStart + rectHeight, Math.Min(sixel.height, scissorYEnd));

        if (minX >= maxX || minY >= maxY) return;

        Span<byte> target = sixel.colorIndexes;
        int bufferWidth = sixel.width;
        int fillLength = maxX - minX;

        // Pre-calculate interpolation bounds (relative to unclipped rectangle height)
        float heightInv = 1.0f / (rectHeight > 1 ? rectHeight - 1 : 1);

        for (int y = minY; y < maxY; y++)
        {
            // Linear interpolation factor t between 0.0 (top) and 1.0 (bottom)
            float t = (y - yStart) * heightInv;
            t = Math.Clamp(t, 0.0f, 1.0f);

            // Interpolate RGBA channels
            byte r = (byte)(topColor.R + t * (bottomColor.R - topColor.R));
            byte g = (byte)(topColor.G + t * (bottomColor.G - topColor.G));
            byte b = (byte)(topColor.B + t * (bottomColor.B - topColor.B));
            byte a = (byte)(topColor.A + t * (bottomColor.A - topColor.A));

            // Skip fully transparent lines
            if (a < TuiSixel.TransparencyThreshold) continue;

            byte colorIndex = Color32ToR3G3B2(new Color32(r, g, b, a));

            int rowOffset = y * bufferWidth + minX;
            target.Slice(rowOffset, fillLength).Fill(colorIndex);
        }
        sixel.UpdatePalette(); // TODO  remove hack
    }
}