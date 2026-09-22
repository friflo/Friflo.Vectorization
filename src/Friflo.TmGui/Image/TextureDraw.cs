// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Runtime.CompilerServices;
using Friflo.TmGui.TUI;
using System.Numerics;

// ReSharper disable UseWithExpressionToCopyStruct
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.Image;


internal sealed class TextureBatch : TmBatch
{
    internal readonly TuiSixel sixel;
    
    internal TextureBatch(TmGuiBackend backend, TmTexture texture) : base(backend)
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

    /// Fast alternative for <see cref="MathF.Round(float)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FastRound(float x)
    {
        return (int)(x + 0.5f);
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
        int xStart      = FastRound(transformedPos.X);
        int yStart      = FastRound(transformedPos.Y);
        int rectWidth   = FastRound(transformedSize.X);
        int rectHeight  = FastRound(transformedSize.Y);

        if (rectWidth <= 0 || rectHeight <= 0) return;

        // Determine scissor region bounds
        int scissorXStart   = FastRound(currentScissor.pos.X);
        int scissorYStart   = FastRound(currentScissor.pos.Y);
        int scissorXEnd     = FastRound(currentScissor.BR.X);
        int scissorYEnd     = FastRound(currentScissor.BR.Y);

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

        sixel.isDirty = true;
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
        int yStart = FastRound(p0.Y);
        int yMid   = FastRound(p1.Y);
        int yEnd   = FastRound(p2.Y);

        if (yStart == yEnd) return; // Degenerate zero-height triangle

        // Calculate Scissor & Buffer intersection bounds
        int scissorYStart = FastRound(currentScissor.pos.Y);
        int scissorYEnd   = FastRound(currentScissor.BR.Y);
        int clipYMin      = Math.Max(0, scissorYStart);
        int clipYMax      = Math.Min(sixel.height, scissorYEnd);

        int scissorXStart = FastRound(currentScissor.pos.X);
        int scissorXEnd   = FastRound(currentScissor.BR.X);
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
        sixel.isDirty = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DrawScanline(Span<byte> target, int bufferWidth, int y, float xA, float xB, int clipXMin, int clipXMax, byte colorIndex)
    {
        if (xA > xB) (xA, xB) = (xB, xA);

        int xStart = Math.Max(FastRound(xA), clipXMin);
        int xEnd   = Math.Min(FastRound(xB), clipXMax);

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

        int xStart     = FastRound(transformedPos.X);
        int yStart     = FastRound(transformedPos.Y);
        int rectWidth  = FastRound(transformedSize.X);
        int rectHeight = FastRound(transformedSize.Y);

        if (rectWidth <= 0 || rectHeight <= 0) return;

        // Determine scissor region bounds
        int scissorXStart = FastRound(currentScissor.pos.X);
        int scissorYStart = FastRound(currentScissor.pos.Y);
        int scissorXEnd   = FastRound(currentScissor.BR.X);
        int scissorYEnd   = FastRound(currentScissor.BR.Y);

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
        sixel.isDirty = true;
    }
    
    internal void StrokeLine(Vector2 start, Vector2 end, float thickness, Color32 color)
    {
        if (color.A < TuiSixel.TransparencyThreshold || thickness <= 0.0f) return;

        if (thickness <= 1.0f)
        {
            // Thin line using 2D Bresenham with clipping
            Vector2 p0 = Vector2.Transform(start, currentTransform);
            Vector2 p1 = Vector2.Transform(end, currentTransform);

            int x0 = FastRound(p0.X);
            int y0 = FastRound(p0.Y);
            int x1 = FastRound(p1.X);
            int y1 = FastRound(p1.Y);

            DrawBresenhamLine(x0, y0, x1, y1, color);
        }
        else
        {
            // Thick line converted into an oriented rectangle (2 triangles)
            Vector2 dir = end - start;
            float len = dir.Length();
            if (len <= 0.0001f) return;

            // Calculate perpendicular normal vector scaled by half-thickness
            Vector2 normal = new Vector2(-dir.Y, dir.X) / len * (thickness * 0.5f);

            Vector2 v0 = start + normal;
            Vector2 v1 = start - normal;
            Vector2 v2 = end - normal;
            Vector2 v3 = end + normal;

            // Render line as two triangles using existing FillTriangle
            FillTriangle(v0, v1, v2, color);
            FillTriangle(v0, v2, v3, color);
        }
        sixel.isDirty = true;
    }

    private void DrawBresenhamLine(int x0, int y0, int x1, int y1, Color32 color)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        int scissorXStart = FastRound(currentScissor.pos.X);
        int scissorYStart = FastRound(currentScissor.pos.Y);
        int scissorXEnd   = FastRound(currentScissor.BR.X);
        int scissorYEnd   = FastRound(currentScissor.BR.Y);

        int clipXMin = Math.Max(0, scissorXStart);
        int clipYMin = Math.Max(0, scissorYStart);
        int clipXMax = Math.Min(sixel.width, scissorXEnd);
        int clipYMax = Math.Min(sixel.height, scissorYEnd);

        byte colorIndex = Color32ToR3G3B2(color);
        Span<byte> target = sixel.colorIndexes;
        int bufferWidth = sixel.width;

        while (true)
        {
            // Clip individual pixels against current scissor and buffer bounds
            if (x0 >= clipXMin && x0 < clipXMax && y0 >= clipYMin && y0 < clipYMax)
            {
                target[y0 * bufferWidth + x0] = colorIndex;
            }

            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
        sixel.isDirty = true;
    }
    
    internal void StrokeRect(Vector2 position, Vector2 size, float thickness, Color32 color)
    {
        if (color.A < TuiSixel.TransparencyThreshold || thickness <= 0.0f || size.X <= 0.0f || size.Y <= 0.0f) return;

        // Clamp thickness to not exceed half of the dimensions
        float maxThickness = Math.Min(size.X, size.Y) * 0.5f;
        float t = Math.Min(thickness, maxThickness);

        // Top edge
        FillRect(position, new Vector2(size.X, t), color);
        
        // Bottom edge
        FillRect(new Vector2(position.X, position.Y + size.Y - t), new Vector2(size.X, t), color);
        
        // Left edge (excluding overlapping corners)
        FillRect(new Vector2(position.X, position.Y + t), new Vector2(t, size.Y - 2 * t), color);
        
        // Right edge (excluding overlapping corners)
        FillRect(new Vector2(position.X + size.X - t, position.Y + t), new Vector2(t, size.Y - 2 * t), color);
    }
    
    internal void FillCircle(Vector2 center, float radius, Color32 color)
    {
        if (color.A < TuiSixel.TransparencyThreshold || radius <= 0.0f) return;

        // Transform center point using current matrix
        Vector2 transformedCenter = Vector2.Transform(center, currentTransform);

        // Calculate radius along transformed axes
        float radiusX = radius * MathF.Abs(currentTransform.M11);
        float radiusY = radius * MathF.Abs(currentTransform.M22);

        if (radiusX <= 0.0f || radiusY <= 0.0f) return;

        int minY = FastRound(transformedCenter.Y - radiusY);
        int maxY = FastRound(transformedCenter.Y + radiusY);

        // Determine scissor region bounds
        int scissorXStart = FastRound(currentScissor.pos.X);
        int scissorYStart = FastRound(currentScissor.pos.Y);
        int scissorXEnd   = FastRound(currentScissor.BR.X);
        int scissorYEnd   = FastRound(currentScissor.BR.Y);

        int clipYMin = Math.Max(0, Math.Max(minY, scissorYStart));
        int clipYMax = Math.Min(sixel.height, Math.Min(maxY, scissorYEnd));

        if (clipYMin >= clipYMax) return;

        int clipXMin = Math.Max(0, scissorXStart);
        int clipXMax = Math.Min(sixel.width, scissorXEnd);

        byte colorIndex = Color32ToR3G3B2(color);
        Span<byte> target = sixel.colorIndexes;
        int bufferWidth = sixel.width;

        float invRadiusYSqr = 1.0f / (radiusY * radiusY);

        for (int y = clipYMin; y < clipYMax; y++)
        {
            // Distance from center on Y axis (using pixel mid-point +0.5f)
            float dy = (y + 0.5f) - transformedCenter.Y;
            float dySqrNorm = (dy * dy) * invRadiusYSqr;

            // Skip rows outside the circle/ellipse equation
            if (dySqrNorm >= 1.0f) continue;

            // Calculate horizontal span width at this Y level
            float dx = radiusX * MathF.Sqrt(1.0f - dySqrNorm);

            int xStart = FastRound(transformedCenter.X - dx);
            int xEnd   = FastRound(transformedCenter.X + dx);

            int minX = Math.Max(xStart, clipXMin);
            int maxX = Math.Min(xEnd, clipXMax);

            if (minX < maxX)
            {
                int rowOffset = y * bufferWidth + minX;
                target.Slice(rowOffset, maxX - minX).Fill(colorIndex);
            }
        }
        sixel.isDirty = true;
    }
    
    internal void StrokeCircle(Vector2 center, float radius, float thickness, Color32 color)
    {
        if (color.A < TuiSixel.TransparencyThreshold || radius <= 0.0f || thickness <= 0.0f) return;

        // Outer and inner radii
        float outerRadius = radius;
        float innerRadius = MathF.Max(0.0f, radius - thickness);

        // If thickness covers the entire circle, delegate to FillCircle
        if (innerRadius <= 0.0f)
        {
            FillCircle(center, radius, color);
            return;
        }

        // Transform center point using current matrix
        Vector2 transformedCenter = Vector2.Transform(center, currentTransform);

        // Calculate outer and inner axes scaled by current transform
        float outerRadiusX = outerRadius * MathF.Abs(currentTransform.M11);
        float outerRadiusY = outerRadius * MathF.Abs(currentTransform.M22);

        float innerRadiusX = innerRadius * MathF.Abs(currentTransform.M11);
        float innerRadiusY = innerRadius * MathF.Abs(currentTransform.M22);

        if (outerRadiusX <= 0.0f || outerRadiusY <= 0.0f) return;

        int minY = FastRound(transformedCenter.Y - outerRadiusY);
        int maxY = FastRound(transformedCenter.Y + outerRadiusY);

        // Determine scissor region bounds
        int scissorXStart = FastRound(currentScissor.pos.X);
        int scissorYStart = FastRound(currentScissor.pos.Y);
        int scissorXEnd   = FastRound(currentScissor.BR.X);
        int scissorYEnd   = FastRound(currentScissor.BR.Y);

        int clipYMin = Math.Max(0, Math.Max(minY, scissorYStart));
        int clipYMax = Math.Min(sixel.height, Math.Min(maxY, scissorYEnd));

        if (clipYMin >= clipYMax) return;

        int clipXMin = Math.Max(0, scissorXStart);
        int clipXMax = Math.Min(sixel.width, scissorXEnd);

        byte colorIndex = Color32ToR3G3B2(color);
        Span<byte> target = sixel.colorIndexes;
        int bufferWidth = sixel.width;

        float invOuterYSqr = 1.0f / (outerRadiusY * outerRadiusY);
        float invInnerYSqr = 1.0f / (innerRadiusY * innerRadiusY);

        for (int y = clipYMin; y < clipYMax; y++)
        {
            float dy = (y + 0.5f) - transformedCenter.Y;

            float dyOuterSqrNorm = (dy * dy) * invOuterYSqr;
            if (dyOuterSqrNorm >= 1.0f) continue;

            // Calculate outer span boundaries
            float dxOuter = outerRadiusX * MathF.Sqrt(1.0f - dyOuterSqrNorm);
            int xOuterStart = FastRound(transformedCenter.X - dxOuter);
            int xOuterEnd   = FastRound(transformedCenter.X + dxOuter);

            // Check if Y line intersects the inner hole
            float dyInnerSqrNorm = (dy * dy) * invInnerYSqr;

            if (dyInnerSqrNorm < 1.0f)
            {
                // Inner hole exists on this Y line -> draw left and right border segments
                float dxInner = innerRadiusX * MathF.Sqrt(1.0f - dyInnerSqrNorm);
                int xInnerStart = FastRound(transformedCenter.X - dxInner);
                int xInnerEnd   = FastRound(transformedCenter.X + dxInner);

                // Left segment: [xOuterStart, xInnerStart]
                int leftMinX = Math.Max(xOuterStart, clipXMin);
                int leftMaxX = Math.Min(xInnerStart, clipXMax);
                if (leftMinX < leftMaxX)
                {
                    target.Slice(y * bufferWidth + leftMinX, leftMaxX - leftMinX).Fill(colorIndex);
                }

                // Right segment: [xInnerEnd, xOuterEnd]
                int rightMinX = Math.Max(xInnerEnd, clipXMin);
                int rightMaxX = Math.Min(xOuterEnd, clipXMax);
                if (rightMinX < rightMaxX)
                {
                    target.Slice(y * bufferWidth + rightMinX, rightMaxX - rightMinX).Fill(colorIndex);
                }
            }
            else
            {
                // Top or bottom cap: solid span across full outer width
                int minX = Math.Max(xOuterStart, clipXMin);
                int maxX = Math.Min(xOuterEnd, clipXMax);
                if (minX < maxX)
                {
                    target.Slice(y * bufferWidth + minX, maxX - minX).Fill(colorIndex);
                }
            }
        }

        sixel.isDirty = true;
    }
    
    // Helper to draw a quad using two triangles
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void FillQuad(Vector2 v0, Vector2 v1, Vector2 v2, Vector2 v3, Color32 color)
    {
        // First triangle (v0 -> v1 -> v2)
        FillTriangle(v0, v1, v2, color);
        
        // Second triangle (v0 -> v2 -> v3)
        FillTriangle(v0, v2, v3, color);
    }

    // Fallback arc rendering for high segment counts
    internal void FillArc(Vector2 center, float radius, float startAngle, float endAngle, Color32 color, int segments)
    {
        if (color.A < TuiSixel.TransparencyThreshold) return;
        if (segments < 1) segments = 1;
        
        float step = (endAngle - startAngle) / segments;

        for (int i = 0; i < segments; i += 2)
        {
            float a0 = startAngle + i * step;
            float a1 = startAngle + (i + 1) * step;
            float a2 = startAngle + (i + 2) * step;

            Vector2 p0 = center + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * radius;
            Vector2 p1 = center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius;
            Vector2 p2 = (i + 2 <= segments)
                ? center + new Vector2(MathF.Cos(a2), MathF.Sin(a2)) * radius
                : p1;

            FillQuad(center, p0, p1, p2, color);
        }
    }
}