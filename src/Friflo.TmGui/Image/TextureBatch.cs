// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Runtime.CompilerServices;
using Friflo.TmGui.TUI;
using System.Numerics;
using Friflo.TmGui.Headless;
using Friflo.TmGui.Session;

// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable MergeIntoPattern
// ReSharper disable InconsistentNaming
// ReSharper disable UseWithExpressionToCopyStruct
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.Image;


internal sealed class TextureBatch : TmBatch
{
    internal readonly TuiSixel sixel;
    
    internal TextureBatch(TmGuiBackend backend, TmTexture texture, FrameTimer frameTimer) : base(backend)
    {
        var tuiTexture = (TuiTexture)texture.native!;
        sixel = tuiTexture.sixel;
        this.frameTimer = frameTimer;
    }

    protected internal override void InitBatch()
    {
    }
    


    /// Fast alternative for <see cref="MathF.Round(float)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FastRound(float x)
    {
        return (int)(x + 0.5f);
    }


    internal void FillRect(Vector2 position, Vector2 size, Color32 color)
    {
        // Early exit for fully transparent rectangles
        if (color.A < TuiSixel.TransparencyThreshold) return;

        // =========================================================================
        // FAST PATH: Axis-Aligned Rectangles (Translation & Scale, no Rotation)
        // =========================================================================
        if (currentTransform.IsAxisAligned())
        {
            // Transform only 2 diagonal corners (TL and BR)
            Vector2 p0 = Vector2.Transform(position, currentTransform);
            Vector2 p2 = Vector2.Transform(position + size, currentTransform);

            // Calculate AABB min/max (handles potential negative scaling/flips)
            int xStart = FastRound(MathF.Min(p0.X, p2.X));
            int yStart = FastRound(MathF.Min(p0.Y, p2.Y));
            int xEnd   = FastRound(MathF.Max(p0.X, p2.X));
            int yEnd   = FastRound(MathF.Max(p0.Y, p2.Y));

            if (xStart >= xEnd || yStart >= yEnd) return;

            // Clip against current scissor rect and screen boundaries
            int minX = Math.Max(xStart, Math.Max(0, FastRound(currentScissor.pos.X)));
            int minY = Math.Max(yStart, Math.Max(0, FastRound(currentScissor.pos.Y)));
            int maxX = Math.Min(xEnd,   Math.Min(sixel.width,  FastRound(currentScissor.BR.X)));
            int maxY = Math.Min(yEnd,   Math.Min(sixel.height, FastRound(currentScissor.BR.Y)));

            if (minX >= maxX || minY >= maxY) return;

            byte colorIndex = TuiSixel.Color32ToR3G3B2(color);
            Span<byte> target = sixel.colorIndexes;
            int bufferWidth   = sixel.width;
            int fillWidth     = maxX - minX;

            // Direct memory fill per scanline (ultra fast, no edge equations)
            for (int y = minY; y < maxY; y++)
            {
                int rowOffset = y * bufferWidth + minX;
                target.Slice(rowOffset, fillWidth).Fill(colorIndex);
            }

            sixel.isDirty = true;
            return;
        }

        // =========================================================================
        // GENERIC PATH: Arbitrary Transforms (Rotation / Shear)
        // =========================================================================
        Vector2 v0 = position;
        Vector2 v1 = new Vector2(position.X + size.X, position.Y);
        Vector2 v2 = position + size;
        Vector2 v3 = new Vector2(position.X, position.Y + size.Y);

        // Delegate rotated rect drawing to FillQuad
        FillQuad(v0, v1, v2, v3, color);
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

        byte colorIndex = TuiSixel.Color32ToR3G3B2(color);
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
        // Early exit if both top and bottom colors are fully transparent
        if (topColor.A < TuiSixel.TransparencyThreshold && bottomColor.A < TuiSixel.TransparencyThreshold) return;
        if (size.X <= 0.0f || size.Y <= 0.0f) return;

        // =========================================================================
        // FAST PATH: Axis-Aligned Rectangles (Scale & Translation, no Rotation)
        // =========================================================================
        if (currentTransform.IsAxisAligned())
        {
            Vector2 p0 = Vector2.Transform(position, currentTransform);
            Vector2 p2 = Vector2.Transform(position + size, currentTransform);

            int xStart = FastRound(MathF.Min(p0.X, p2.X));
            int yStart = FastRound(MathF.Min(p0.Y, p2.Y));
            int xEnd   = FastRound(MathF.Max(p0.X, p2.X));
            int yEnd   = FastRound(MathF.Max(p0.Y, p2.Y));

            if (xStart >= xEnd || yStart >= yEnd) return;

            // Clip against current scissor rect and screen boundaries
            int minX = Math.Max(xStart, Math.Max(0, FastRound(currentScissor.pos.X)));
            int minY = Math.Max(yStart, Math.Max(0, FastRound(currentScissor.pos.Y)));
            int maxX = Math.Min(xEnd,   Math.Min(sixel.width,  FastRound(currentScissor.BR.X)));
            int maxY = Math.Min(yEnd,   Math.Min(sixel.height, FastRound(currentScissor.BR.Y)));

            if (minX >= maxX || minY >= maxY) return;

            Span<byte> target = sixel.colorIndexes;
            int bufferWidth   = sixel.width;
            int fillLength    = maxX - minX;

            float heightInv = 1.0f / MathF.Max(1.0f, p2.Y - p0.Y);

            for (int y = minY; y < maxY; y++)
            {
                float t = Math.Clamp((y + 0.5f - p0.Y) * heightInv, 0.0f, 1.0f);

                byte a = (byte)(topColor.A + t * (bottomColor.A - topColor.A));
                if (a < TuiSixel.TransparencyThreshold) continue;

                byte r = (byte)(topColor.R + t * (bottomColor.R - topColor.R));
                byte g = (byte)(topColor.G + t * (bottomColor.G - topColor.G));
                byte b = (byte)(topColor.B + t * (bottomColor.B - topColor.B));

                byte colorIndex = TuiSixel.Color32ToR3G3B2(new Color32(r, g, b));

                int rowOffset = y * bufferWidth + minX;
                target.Slice(rowOffset, fillLength).Fill(colorIndex);
            }

            sixel.isDirty = true;
            return;
        }

        // =========================================================================
        // GENERIC PATH: Arbitrary Transforms (Rotation / Shear via Inverse Mapping)
        // =========================================================================
        if (!Matrix4x4.Invert(currentTransform, out Matrix4x4 invTransform)) return;

        // Transform all 4 corners to find screen AABB
        Vector2 v0 = position;
        Vector2 v1 = new Vector2(position.X + size.X, position.Y);
        Vector2 v2 = position + size;
        Vector2 v3 = new Vector2(position.X, position.Y + size.Y);

        Vector2 gP0 = Vector2.Transform(v0, currentTransform);
        Vector2 gP1 = Vector2.Transform(v1, currentTransform);
        Vector2 gP2 = Vector2.Transform(v2, currentTransform);
        Vector2 gP3 = Vector2.Transform(v3, currentTransform);

        float minXFloat = MathF.Min(MathF.Min(gP0.X, gP1.X), MathF.Min(gP2.X, gP3.X));
        float maxXFloat = MathF.Max(MathF.Max(gP0.X, gP1.X), MathF.Max(gP2.X, gP3.X));
        float minYFloat = MathF.Min(MathF.Min(gP0.Y, gP1.Y), MathF.Min(gP2.Y, gP3.Y));
        float maxYFloat = MathF.Max(MathF.Max(gP0.Y, gP1.Y), MathF.Max(gP2.Y, gP3.Y));

        int gXStart = FastRound(minXFloat);
        int gYStart = FastRound(minYFloat);
        int gXEnd   = FastRound(maxXFloat);
        int gYEnd   = FastRound(maxYFloat);

        if (gXStart >= gXEnd || gYStart >= gYEnd) return;

        int gMinX = Math.Max(gXStart, Math.Max(0, FastRound(currentScissor.pos.X)));
        int gMinY = Math.Max(gYStart, Math.Max(0, FastRound(currentScissor.pos.Y)));
        int gMaxX = Math.Min(gXEnd,   Math.Min(sixel.width,  FastRound(currentScissor.BR.X)));
        int gMaxY = Math.Min(gYEnd,   Math.Min(sixel.height, FastRound(currentScissor.BR.Y)));

        if (gMinX >= gMaxX || gMinY >= gMaxY) return;

        Span<byte> gTarget = sixel.colorIndexes;
        int gBufferWidth   = sixel.width;

        float localMinY = position.Y;
        float localMaxY = position.Y + size.Y;
        float localMinX = position.X;
        float localMaxX = position.X + size.X;
        float heightRecip = 1.0f / size.Y;

        for (int y = gMinY; y < gMaxY; y++)
        {
            int rowOffset = y * gBufferWidth;

            for (int x = gMinX; x < gMaxX; x++)
            {
                Vector2 screenPos = new Vector2(x + 0.5f, y + 0.5f);
                Vector2 localPos  = Vector2.Transform(screenPos, invTransform);

                // Bounds check in local space
                if (localPos.X < localMinX || localPos.X > localMaxX ||
                    localPos.Y < localMinY || localPos.Y > localMaxY) continue;

                float t = Math.Clamp((localPos.Y - localMinY) * heightRecip, 0.0f, 1.0f);

                byte a = (byte)(topColor.A + t * (bottomColor.A - topColor.A));
                if (a < TuiSixel.TransparencyThreshold) continue;

                byte r = (byte)(topColor.R + t * (bottomColor.R - topColor.R));
                byte g = (byte)(topColor.G + t * (bottomColor.G - topColor.G));
                byte b = (byte)(topColor.B + t * (bottomColor.B - topColor.B));

                gTarget[rowOffset + x] = TuiSixel.Color32ToR3G3B2(new Color32(r, g, b));
            }
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

        byte colorIndex = TuiSixel.Color32ToR3G3B2(color);
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
        // Check alpha early
        if (color.A < TuiSixel.TransparencyThreshold || radius <= 0f) return;

        // =========================================================================
        // FAST PATH: Axis-Aligned / Uniform Scale (Circle stays a Circle)
        // =========================================================================
        if (currentTransform.IsAxisAligned())
        {
            // Transform center point to screen space
            Vector2 screenCenter = Vector2.Transform(center, currentTransform);

            // Scale radius using matrix scale factor (handles uniform scale correctly)
            float scaleX = MathF.Abs(currentTransform.M11);
            float scaleY = MathF.Abs(currentTransform.M22);

            // Fast path requires roughly uniform scale to remain a perfect circle
            if (MathF.Abs(scaleX - scaleY) < 0.001f)
            {
                float scaledRadius = radius * scaleX;
                float r2 = scaledRadius * scaledRadius;

                // Compute screen AABB bounds
                int xStart = FastRound(screenCenter.X - scaledRadius);
                int yStart = FastRound(screenCenter.Y - scaledRadius);
                int xEnd   = FastRound(screenCenter.X + scaledRadius);
                int yEnd   = FastRound(screenCenter.Y + scaledRadius);

                if (xStart >= xEnd || yStart >= yEnd) return;

                // Clip against scissor rect and screen bounds
                int minX = Math.Max(xStart, Math.Max(0, FastRound(currentScissor.pos.X)));
                int minY = Math.Max(yStart, Math.Max(0, FastRound(currentScissor.pos.Y)));
                int maxX = Math.Min(xEnd,   Math.Min(sixel.width,  FastRound(currentScissor.BR.X)));
                int maxY = Math.Min(yEnd,   Math.Min(sixel.height, FastRound(currentScissor.BR.Y)));

                if (minX >= maxX || minY >= maxY) return;

                byte colorIndex = TuiSixel.Color32ToR3G3B2(color);
                Span<byte> target = sixel.colorIndexes;
                int bufferWidth   = sixel.width;

                // Fast circle rasterization using squared distance check (dx^2 + dy^2 <= r^2)
                for (int y = minY; y < maxY; y++)
                {
                    float dy = (y + 0.5f) - screenCenter.Y;
                    float dy2 = dy * dy;
                    int rowOffset = y * bufferWidth;

                    for (int x = minX; x < maxX; x++)
                    {
                        float dx = (x + 0.5f) - screenCenter.X;
                        if (dx * dx + dy2 <= r2)
                        {
                            target[rowOffset + x] = colorIndex;
                        }
                    }
                }

                sixel.isDirty = true;
                return;
            }
        }

        // =========================================================================
        // GENERIC PATH: Arbitrary Transforms (Out-of-line)
        // =========================================================================
        FillCircleGeneric(center, radius, color);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void FillCircleGeneric(Vector2 center, float radius, Color32 color)
    {
        if (!Matrix4x4.Invert(currentTransform, out Matrix4x4 invTransform)) return;

        // Estimate screen AABB by transforming 4 bounding box corners of the local circle
        Vector2 localMin = center - new Vector2(radius);
        Vector2 localMax = center + new Vector2(radius);

        Vector2 p0 = Vector2.Transform(new Vector2(localMin.X, localMin.Y), currentTransform);
        Vector2 p1 = Vector2.Transform(new Vector2(localMax.X, localMin.Y), currentTransform);
        Vector2 p2 = Vector2.Transform(new Vector2(localMax.X, localMax.Y), currentTransform);
        Vector2 p3 = Vector2.Transform(new Vector2(localMin.X, localMax.Y), currentTransform);

        float minXFloat = MathF.Min(MathF.Min(p0.X, p1.X), MathF.Min(p2.X, p3.X));
        float maxXFloat = MathF.Max(MathF.Max(p0.X, p1.X), MathF.Max(p2.X, p3.X));
        float minYFloat = MathF.Min(MathF.Min(p0.Y, p1.Y), MathF.Min(p2.Y, p3.Y));
        float maxYFloat = MathF.Max(MathF.Max(p0.Y, p1.Y), MathF.Max(p2.Y, p3.Y));

        int gXStart = FastRound(minXFloat);
        int gYStart = FastRound(minYFloat);
        int gXEnd   = FastRound(maxXFloat);
        int gYEnd   = FastRound(maxYFloat);

        if (gXStart >= gXEnd || gYStart >= gYEnd) return;

        int gMinX = Math.Max(gXStart, Math.Max(0, FastRound(currentScissor.pos.X)));
        int gMinY = Math.Max(gYStart, Math.Max(0, FastRound(currentScissor.pos.Y)));
        int gMaxX = Math.Min(gXEnd,   Math.Min(sixel.width,  FastRound(currentScissor.BR.X)));
        int gMaxY = Math.Min(gYEnd,   Math.Min(sixel.height, FastRound(currentScissor.BR.Y)));

        if (gMinX >= gMaxX || gMinY >= gMaxY) return;

        float rSq = radius * radius;
        byte gColorIndex = TuiSixel.Color32ToR3G3B2(color);
        Span<byte> gTarget = sixel.colorIndexes;
        int gBufferWidth   = sixel.width;

        // Inverse mapping: Map screen pixel back to local space and test distance to local center
        for (int y = gMinY; y < gMaxY; y++)
        {
            int rowOffset = y * gBufferWidth;

            for (int x = gMinX; x < gMaxX; x++)
            {
                Vector2 screenPos = new Vector2(x + 0.5f, y + 0.5f);
                Vector2 localPos  = Vector2.Transform(screenPos, invTransform);

                if (Vector2.DistanceSquared(localPos, center) <= rSq)
                {
                    gTarget[rowOffset + x] = gColorIndex;
                }
            }
        }

        sixel.isDirty = true;
    }
    
    internal void StrokeCircle(Vector2 center, float radius, float thickness, Color32 color)
    {
        if (color.A < TuiSixel.TransparencyThreshold || radius <= 0.0f || thickness <= 0.0f) return;

        float outerRadius = radius;
        float innerRadius = MathF.Max(0.0f, radius - thickness);

        // If thickness covers the entire circle, delegate to FillCircle
        if (innerRadius <= 0.0f)
        {
            FillCircle(center, radius, color);
            return;
        }

        // =========================================================================
        // FAST PATH: Axis-Aligned / Scale & Translation (Scanline-based Fill)
        // =========================================================================
        if (currentTransform.IsAxisAligned())
        {
            Vector2 transformedCenter = Vector2.Transform(center, currentTransform);

            float outerRadiusX = outerRadius * MathF.Abs(currentTransform.M11);
            float outerRadiusY = outerRadius * MathF.Abs(currentTransform.M22);

            float innerRadiusX = innerRadius * MathF.Abs(currentTransform.M11);
            float innerRadiusY = innerRadius * MathF.Abs(currentTransform.M22);

            if (outerRadiusX <= 0.0f || outerRadiusY <= 0.0f) return;

            int minY = FastRound(transformedCenter.Y - outerRadiusY);
            int maxY = FastRound(transformedCenter.Y + outerRadiusY);

            int scissorXStart = FastRound(currentScissor.pos.X);
            int scissorYStart = FastRound(currentScissor.pos.Y);
            int scissorXEnd   = FastRound(currentScissor.BR.X);
            int scissorYEnd   = FastRound(currentScissor.BR.Y);

            int clipYMin = Math.Max(0, Math.Max(minY, scissorYStart));
            int clipYMax = Math.Min(sixel.height, Math.Min(maxY, scissorYEnd));

            if (clipYMin >= clipYMax) return;

            int clipXMin = Math.Max(0, scissorXStart);
            int clipXMax = Math.Min(sixel.width, scissorXEnd);

            byte colorIndex = TuiSixel.Color32ToR3G3B2(color);
            Span<byte> target = sixel.colorIndexes;
            int bufferWidth = sixel.width;

            float invOuterYSqr = 1.0f / (outerRadiusY * outerRadiusY);
            float invInnerYSqr = 1.0f / (innerRadiusY * innerRadiusY);

            for (int y = clipYMin; y < clipYMax; y++)
            {
                float dy = (y + 0.5f) - transformedCenter.Y;

                float dyOuterSqrNorm = (dy * dy) * invOuterYSqr;
                if (dyOuterSqrNorm >= 1.0f) continue;

                float dxOuter = outerRadiusX * MathF.Sqrt(1.0f - dyOuterSqrNorm);
                int xOuterStart = FastRound(transformedCenter.X - dxOuter);
                int xOuterEnd   = FastRound(transformedCenter.X + dxOuter);

                float dyInnerSqrNorm = (dy * dy) * invInnerYSqr;

                if (dyInnerSqrNorm < 1.0f)
                {
                    float dxInner = innerRadiusX * MathF.Sqrt(1.0f - dyInnerSqrNorm);
                    int xInnerStart = FastRound(transformedCenter.X - dxInner);
                    int xInnerEnd   = FastRound(transformedCenter.X + dxInner);

                    // Left segment
                    int leftMinX = Math.Max(xOuterStart, clipXMin);
                    int leftMaxX = Math.Min(xInnerStart, clipXMax);
                    if (leftMinX < leftMaxX)
                    {
                        target.Slice(y * bufferWidth + leftMinX, leftMaxX - leftMinX).Fill(colorIndex);
                    }

                    // Right segment
                    int rightMinX = Math.Max(xInnerEnd, clipXMin);
                    int rightMaxX = Math.Min(xOuterEnd, clipXMax);
                    if (rightMinX < rightMaxX)
                    {
                        target.Slice(y * bufferWidth + rightMinX, rightMaxX - rightMinX).Fill(colorIndex);
                    }
                }
                else
                {
                    // Top or bottom cap
                    int minX = Math.Max(xOuterStart, clipXMin);
                    int maxX = Math.Min(xOuterEnd, clipXMax);
                    if (minX < maxX)
                    {
                        target.Slice(y * bufferWidth + minX, maxX - minX).Fill(colorIndex);
                    }
                }
            }

            sixel.isDirty = true;
            return;
        }

        // =========================================================================
        // GENERIC PATH: Arbitrary Transforms (Out-of-line)
        // =========================================================================
        StrokeCircleGeneric(center, outerRadius, innerRadius, color);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void StrokeCircleGeneric(Vector2 center, float outerRadius, float innerRadius, Color32 color)
    {
        if (!Matrix4x4.Invert(currentTransform, out Matrix4x4 invTransform)) return;

        // Estimate screen AABB bounds of the rotated circle outer edge
        Vector2 localMin = center - new Vector2(outerRadius);
        Vector2 localMax = center + new Vector2(outerRadius);

        Vector2 p0 = Vector2.Transform(new Vector2(localMin.X, localMin.Y), currentTransform);
        Vector2 p1 = Vector2.Transform(new Vector2(localMax.X, localMin.Y), currentTransform);
        Vector2 p2 = Vector2.Transform(new Vector2(localMax.X, localMax.Y), currentTransform);
        Vector2 p3 = Vector2.Transform(new Vector2(localMin.X, localMax.Y), currentTransform);

        float minXFloat = MathF.Min(MathF.Min(p0.X, p1.X), MathF.Min(p2.X, p3.X));
        float maxXFloat = MathF.Max(MathF.Max(p0.X, p1.X), MathF.Max(p2.X, p3.X));
        float minYFloat = MathF.Min(MathF.Min(p0.Y, p1.Y), MathF.Min(p2.Y, p3.Y));
        float maxYFloat = MathF.Max(MathF.Max(p0.Y, p1.Y), MathF.Max(p2.Y, p3.Y));

        int gXStart = FastRound(minXFloat);
        int gYStart = FastRound(minYFloat);
        int gXEnd   = FastRound(maxXFloat);
        int gYEnd   = FastRound(maxYFloat);

        if (gXStart >= gXEnd || gYStart >= gYEnd) return;

        int gMinX = Math.Max(gXStart, Math.Max(0, FastRound(currentScissor.pos.X)));
        int gMinY = Math.Max(gYStart, Math.Max(0, FastRound(currentScissor.pos.Y)));
        int gMaxX = Math.Min(gXEnd,   Math.Min(sixel.width,  FastRound(currentScissor.BR.X)));
        int gMaxY = Math.Min(gYEnd,   Math.Min(sixel.height, FastRound(currentScissor.BR.Y)));

        if (gMinX >= gMaxX || gMinY >= gMaxY) return;

        float outerRadiusSq = outerRadius * outerRadius;
        float innerRadiusSq = innerRadius * innerRadius;

        byte gColorIndex = TuiSixel.Color32ToR3G3B2(color);
        Span<byte> gTarget = sixel.colorIndexes;
        int gBufferWidth   = sixel.width;

        // Inverse mapping loop: Check squared distance in untransformed local space
        for (int y = gMinY; y < gMaxY; y++)
        {
            int rowOffset = y * gBufferWidth;

            for (int x = gMinX; x < gMaxX; x++)
            {
                Vector2 screenPos = new Vector2(x + 0.5f, y + 0.5f);
                Vector2 localPos  = Vector2.Transform(screenPos, invTransform);

                float distSq = Vector2.DistanceSquared(localPos, center);

                if (distSq <= outerRadiusSq && distSq >= innerRadiusSq)
                {
                    gTarget[rowOffset + x] = gColorIndex;
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


    
    // --------------------------------------------------- Sprite ---------------------------------------------------
    
    private static void GetImageProperties (in TmTexture texture, out byte[] rgbaPixels, out int width, out int height)
    {
        if (texture.native is HeadlessTexture tex) {
            rgbaPixels  = tex.rgbaPixels;
            width       = tex.width;
            height      = tex.height;
            return;
        }
        if (texture.native is TuiTexture tuiTex) {
            rgbaPixels  = tuiTex.data;
            width       = tuiTex.width;
            height      = tuiTex.height;
            return;
        }
        throw new NotSupportedException("texture not supported");
    }
    
    internal void DrawSprite(in TmTexture texture, in VertexQuad quad, Color32 color)
    {
        vertexCount = 0; // NOTE! Prevent growing of vertices
        
        // Check global color alpha early
        if (color.A < TuiSixel.TransparencyThreshold) return;

        GetImageProperties(texture, out byte[] rgbaPixels, out int texWidth, out int texHeight);
        ReadOnlySpan<byte> srcPixels = rgbaPixels;

        byte tintR = color.R;
        byte tintG = color.G;
        byte tintB = color.B;

        Span<byte> target = sixel.colorIndexes;
        int bufferWidth   = sixel.width;

        // =========================================================================
        // FAST PATH: Axis-Aligned (Translation & Scale only, no rotation/skew)
        // =========================================================================
        if (currentTransform.IsAxisAligned())
        {
            // Transform only 2 diagonal corners to get AABB
            Vector2 p0 = Vector2.Transform(quad[0].position, currentTransform);
            Vector2 p2 = Vector2.Transform(quad[2].position, currentTransform);

            int xStart = FastRound(p0.X);
            int yStart = FastRound(p0.Y);
            int xEnd   = FastRound(p2.X);
            int yEnd   = FastRound(p2.Y);

            if (xStart >= xEnd || yStart >= yEnd) return;

            // Scissor & Screen bounds clipping
            int minX = Math.Max(xStart, Math.Max(0, FastRound(currentScissor.pos.X)));
            int minY = Math.Max(yStart, Math.Max(0, FastRound(currentScissor.pos.Y)));
            int maxX = Math.Min(xEnd,   Math.Min(sixel.width,  FastRound(currentScissor.BR.X)));
            int maxY = Math.Min(yEnd,   Math.Min(sixel.height, FastRound(currentScissor.BR.Y)));

            if (minX >= maxX || minY >= maxY) return;

            Vector2 uv0 = quad[0].uv;
            Vector2 uv2 = quad[2].uv;

            float quadWidthRecip  = 1.0f / (p2.X - p0.X);
            float quadHeightRecip = 1.0f / (p2.Y - p0.Y);

            for (int y = minY; y < maxY; y++)
            {
                float vNorm = (y - p0.Y) * quadHeightRecip;
                float v = uv0.Y + vNorm * (uv2.Y - uv0.Y);
                int texY = Math.Clamp((int)(v * texHeight), 0, texHeight - 1);
                int texRowOffset = texY * texWidth * 4;
                int rowOffset = y * bufferWidth;

                for (int x = minX; x < maxX; x++)
                {
                    float uNorm = (x - p0.X) * quadWidthRecip;
                    float u = uv0.X + uNorm * (uv2.X - uv0.X);
                    int texX = Math.Clamp((int)(u * texWidth), 0, texWidth - 1);

                    int pixelIdx = texRowOffset + (texX * 4);

                    byte a = srcPixels[pixelIdx + 3];
                    if (a < TuiSixel.TransparencyThreshold) continue;

                    byte r = (byte)((srcPixels[pixelIdx]     * tintR) >> 8);
                    byte g = (byte)((srcPixels[pixelIdx + 1] * tintG) >> 8);
                    byte b = (byte)((srcPixels[pixelIdx + 2] * tintB) >> 8);

                    target[rowOffset + x] = TuiSixel.Color32ToR3G3B2(new Color32(r, g, b));
                }
            }

            sixel.isDirty = true;
            return;
        }

        // =========================================================================
        // GENERIC PATH: Arbitrary Transforms
        // =========================================================================
        DrawSpriteGeneric(srcPixels, texWidth, texHeight, quad, tintR, tintG, tintB, target, bufferWidth);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void DrawSpriteGeneric(
        ReadOnlySpan<byte> srcPixels, 
        int texWidth, 
        int texHeight, 
        in VertexQuad quad, 
        byte tintR, 
        byte tintG, 
        byte tintB, 
        Span<byte> target, 
        int bufferWidth)
    {
        if (!Matrix4x4.Invert(currentTransform, out Matrix4x4 invTransform)) return;

        // Transform ALL 4 corners to find true screen AABB
        Vector2 gP0 = Vector2.Transform(quad[0].position, currentTransform);
        Vector2 gP1 = Vector2.Transform(quad[1].position, currentTransform);
        Vector2 gP2 = Vector2.Transform(quad[2].position, currentTransform);
        Vector2 gP3 = Vector2.Transform(quad[3].position, currentTransform);

        float minXFloat = MathF.Min(MathF.Min(gP0.X, gP1.X), MathF.Min(gP2.X, gP3.X));
        float maxXFloat = MathF.Max(MathF.Max(gP0.X, gP1.X), MathF.Max(gP2.X, gP3.X));
        float minYFloat = MathF.Min(MathF.Min(gP0.Y, gP1.Y), MathF.Min(gP2.Y, gP3.Y));
        float maxYFloat = MathF.Max(MathF.Max(gP0.Y, gP1.Y), MathF.Max(gP2.Y, gP3.Y));

        int gXStart = FastRound(minXFloat);
        int gYStart = FastRound(minYFloat);
        int gXEnd   = FastRound(maxXFloat);
        int gYEnd   = FastRound(maxYFloat);

        if (gXStart >= gXEnd || gYStart >= gYEnd) return;

        int gMinX = Math.Max(gXStart, Math.Max(0, FastRound(currentScissor.pos.X)));
        int gMinY = Math.Max(gYStart, Math.Max(0, FastRound(currentScissor.pos.Y)));
        int gMaxX = Math.Min(gXEnd,   Math.Min(sixel.width,  FastRound(currentScissor.BR.X)));
        int gMaxY = Math.Min(gYEnd,   Math.Min(sixel.height, FastRound(currentScissor.BR.Y)));

        if (gMinX >= gMaxX || gMinY >= gMaxY) return;

        Vector2 localTL = quad[0].position;
        Vector2 localBR = quad[2].position;

        Vector2 gUv0 = quad[0].uv;
        Vector2 gUv2 = quad[2].uv;

        float localWidthRecip  = 1.0f / (localBR.X - localTL.X);
        float localHeightRecip = 1.0f / (localBR.Y - localTL.Y);

        for (int y = gMinY; y < gMaxY; y++)
        {
            int rowOffset = y * bufferWidth;

            for (int x = gMinX; x < gMaxX; x++)
            {
                // Map current screen pixel back to local untransformed quad space
                Vector2 screenPos = new Vector2(x + 0.5f, y + 0.5f);
                Vector2 localPos  = Vector2.Transform(screenPos, invTransform);

                // Calculate normalized local coordinates [0.0 - 1.0]
                float uNorm = (localPos.X - localTL.X) * localWidthRecip;
                float vNorm = (localPos.Y - localTL.Y) * localHeightRecip;

                // Reject pixels outside local quad bounds
                if (uNorm < 0.0f || uNorm > 1.0f || vNorm < 0.0f || vNorm > 1.0f) continue;

                float u = gUv0.X + uNorm * (gUv2.X - gUv0.X);
                float v = gUv0.Y + vNorm * (gUv2.Y - gUv0.Y);

                int texX = Math.Clamp((int)(u * texWidth),  0, texWidth - 1);
                int texY = Math.Clamp((int)(v * texHeight), 0, texHeight - 1);

                int pixelIdx = (texY * texWidth + texX) * 4;

                byte a = srcPixels[pixelIdx + 3];
                if (a < TuiSixel.TransparencyThreshold) continue;

                byte r = (byte)((srcPixels[pixelIdx]     * tintR) >> 8);
                byte g = (byte)((srcPixels[pixelIdx + 1] * tintG) >> 8);
                byte b = (byte)((srcPixels[pixelIdx + 2] * tintB) >> 8);

                target[rowOffset + x] = TuiSixel.Color32ToR3G3B2(new Color32(r, g, b));
            }
        }

        sixel.isDirty = true;
    }
}

internal static class TransformExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAxisAligned(in this Matrix4x4 m)
    {
        // No rotation/skew components
        return m.M12 == 0f && m.M21 == 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTranslationOnly(in this Matrix4x4 m)
    {
        return m.M11 == 1f && m.M22 == 1f && m.M12 == 0f && m.M21 == 0f;
    }
}