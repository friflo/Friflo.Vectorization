// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming
// ReSharper disable UseWithExpressionToCopyStruct
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable MergeIntoPattern
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public readonly ref partial struct TmDraw
{
    public void FillRect(Vector2 position, Vector2 size, Color32 color)
    {
        var bat = batch;
        var texView = bat.currentTexture.hasWhitePixel ? bat.currentTexture : bat.currentFontTexture;
        if (bat.vertexCount + 4 > bat.vertexBuffer.Length || bat.currentTexture != texView) {
            bat.Flush();
            bat.currentTexture = texView;
        }
        var uv = texView.whiteUv;

        float x0 = position.X;
        float y0 = position.Y;
        float x1 = x0 + size.X;
        float y1 = y0 + size.Y;

        var packed   = color.Packed;
        ref var quad = ref AddQuad();
        quad[0] = new Vertex2D(new Vector2(x0, y0), uv, packed);
        quad[1] = new Vertex2D(new Vector2(x1, y0), uv, packed);
        quad[2] = new Vertex2D(new Vector2(x1, y1), uv, packed);
        quad[3] = new Vertex2D(new Vector2(x0, y1), uv, packed);
    }

    /// <summary>
    /// Draws a rectangle with per-corner gradient colors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillRectGradient(Vector2 position, Vector2 size, Color32 topLeft, Color32 topRight, Color32 bottomRight, Color32 bottomLeft)
    {
        var bat = batch;
        var texView = bat.currentTexture.hasWhitePixel ? bat.currentTexture : bat.currentFontTexture;
        if (bat.vertexCount + 4 > bat.vertexBuffer.Length || bat.currentTexture != texView) {
            bat.Flush();
            bat.currentTexture = texView;
        }
        var uv = bat.currentTexture.whiteUv;

        float x1 = position.X;
        float y1 = position.Y;
        float x2 = position.X + size.X;
        float y2 = position.Y + size.Y;

        ref var quad = ref AddQuad();
        quad[0] = new Vertex2D(new Vector2(x1, y1), uv, topLeft.Packed);
        quad[1] = new Vertex2D(new Vector2(x2, y1), uv, topRight.Packed);
        quad[2] = new Vertex2D(new Vector2(x2, y2), uv, bottomRight.Packed);
        quad[3] = new Vertex2D(new Vector2(x1, y2), uv, bottomLeft.Packed);
    }

    /// <summary>
    /// Draws a vertical gradient rectangle (top to bottom).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillRectGradientVertical(Vector2 position, Vector2 size, Color32 top, Color32 bottom)
        => FillRectGradient(position, size, top, top, bottom, bottom);



    /// <summary>
    /// Draws a single filled triangle using a quad slot (duplicates 3rd vertex).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillTriangle(Vector2 v0, Vector2 v1, Vector2 v2, Color32 color)
    {
        if (color.A == 0) return;
        FillQuad(v0, v1, v2, v2, color);
    }

    /// <summary>
    /// Draws a thick line segment between two points.
    /// </summary>
    public void StrokeLine(Vector2 start, Vector2 end, float thickness, Color32 color)
    {
        if (color.A == 0) return;
        Vector2 dir = end - start;
        float len = dir.Length();
        if (len < 0.0001f) return;

        // Normal vector perpendicular to the line
        Vector2 normal = new Vector2(-dir.Y, dir.X) / len * (thickness * 0.5f);

        FillQuad(
            start + normal, // V0: Top-Left
            end   + normal, // V1: Top-Right
            end   - normal, // V2: Bottom-Right
            start - normal, // V3: Bottom-Left
            color
        );
    }

    /// <summary>
    /// Draws an un-filled rectangle outline.
    /// </summary>
    public void StrokeRect(Vector2 position, Vector2 size, float thickness, Color32 color)
    {
        if (color.A == 0) return;
        float x = position.X;
        float y = position.Y;
        float w = size.X;
        float h = size.Y;

        // Top, Right, Bottom, Left edges
        FillRect(new Vector2(x, y),                             new Vector2(w, thickness),                  color);
        FillRect(new Vector2(x + w - thickness, y + thickness), new Vector2(thickness, h - thickness * 2f), color);
        FillRect(new Vector2(x, y + h - thickness),             new Vector2(w, thickness),                  color);
        FillRect(new Vector2(x, y + thickness),                 new Vector2(thickness, h - thickness * 2f), color);
    }
    
// Source code comments must always be in English

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void StrokeCornerArc(Vector2 center, float innerRadius, float outerRadius, ArcCorner corner, Color32 color, int segments)
    {
        if (segments < 1) segments = 1;

        if (segments <= ArcLookups.CornerTableLength)
        {
            ReadOnlySpan<Vector2> lookup = ArcLookups.CornerTables[segments];
            ArcLookups.GetCornerTransform(corner, out float signX, out float signY, out bool swapXY);

            for (int i = 0; i < segments; i++)
            {
                Vector2 u0 = lookup[i];
                Vector2 u1 = lookup[i + 1];

                Vector2 dir0 = swapXY ? new Vector2(u0.Y * signX, u0.X * signY) : new Vector2(u0.X * signX, u0.Y * signY);
                Vector2 dir1 = swapXY ? new Vector2(u1.Y * signX, u1.X * signY) : new Vector2(u1.X * signX, u1.Y * signY);

                Vector2 inner0 = center + dir0 * innerRadius;
                Vector2 outer0 = center + dir0 * outerRadius;
                Vector2 inner1 = center + dir1 * innerRadius;
                Vector2 outer1 = center + dir1 * outerRadius;

                FillQuad(inner0, outer0, outer1, inner1, color);
            }
            return;
        }

        var (startAngle, endAngle) = ArcLookups.GetCornerAngles(corner);
        StrokeArc(center, innerRadius, outerRadius, startAngle, endAngle, color, segments);
    }
    
    public void StrokeArc(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, Color32 color, int segments)
    {
        if (color.A == 0) return;
        if (segments < 1) segments = 1;
        float step = (endAngle - startAngle) / segments;

        for (int i = 0; i < segments; i++)
        {
            float a0 = startAngle + i * step;
            float a1 = startAngle + (i + 1) * step;

            float cos0 = MathF.Cos(a0), sin0 = MathF.Sin(a0);
            float cos1 = MathF.Cos(a1), sin1 = MathF.Sin(a1);

            Vector2 outer0 = center + new Vector2(cos0, sin0) * outerRadius;
            Vector2 inner0 = center + new Vector2(cos0, sin0) * innerRadius;
            Vector2 outer1 = center + new Vector2(cos1, sin1) * outerRadius;
            Vector2 inner1 = center + new Vector2(cos1, sin1) * innerRadius;
            FillQuad(inner0, outer0, outer1, inner1, color);
        }
    }
    
    /// <summary>
    /// Draws an un-filled rounded rectangle outline.
    /// </summary>
    public void StrokeRectRounded(Vector2 position, Vector2 size, float radius, float thickness, Color32 color, int segments = 8)
    {
        if (color.A == 0) return;
        if (thickness <= 0f) return;

        if (radius <= 0f) {
            StrokeRect(position, size, thickness, color);
            return;
        }

        radius      = MathF.Min(radius, MathF.Min(size.X, size.Y) * 0.5f);
        thickness   = MathF.Min(thickness, radius);

        float innerRadius = radius - thickness;
        float x = position.X;
        float y = position.Y;
        float w = size.X;
        float h = size.Y;

        FillRect(new Vector2(x + radius, y),                  new Vector2(w - radius * 2f, thickness), color); // Top
        FillRect(new Vector2(x + radius, y + h - thickness),  new Vector2(w - radius * 2f, thickness), color); // Bottom
        FillRect(new Vector2(x, y + radius),                  new Vector2(thickness, h - radius * 2f), color); // Left
        FillRect(new Vector2(x + w - thickness, y + radius),  new Vector2(thickness, h - radius * 2f), color); // Right

        // 4 node centers
        Vector2 cTl = position + new Vector2(radius,          radius);
        Vector2 cTr = position + new Vector2(size.X - radius, radius);
        Vector2 cBr = position + new Vector2(size.X - radius, size.Y - radius);
        Vector2 cBl = position + new Vector2(radius,          size.Y - radius);

        if (innerRadius <= 0f) {
            FillCornerArc(cTl, radius, ArcCorner.TopLeft,     color, segments);
            FillCornerArc(cTr, radius, ArcCorner.TopRight,    color, segments);
            FillCornerArc(cBr, radius, ArcCorner.BottomRight, color, segments);
            FillCornerArc(cBl, radius, ArcCorner.BottomLeft,  color, segments);
            return;
        }
        StrokeCornerArc(cTl, innerRadius, radius, ArcCorner.TopLeft,     color, segments);
        StrokeCornerArc(cTr, innerRadius, radius, ArcCorner.TopRight,    color, segments);
        StrokeCornerArc(cBr, innerRadius, radius, ArcCorner.BottomRight, color, segments);
        StrokeCornerArc(cBl, innerRadius, radius, ArcCorner.BottomLeft,  color, segments);
    }
    

    /// <summary>
    /// Draws a filled rounded rectangle.
    /// </summary>
    public void FillRectRounded(Vector2 position, Vector2 size, float radius, Color32 color, int segments = 8)
    {
        if (color.A == 0) return;
        if (radius <= 0f) {
            FillRect(position, size, color);
            return;
        }

        radius = MathF.Min(radius, MathF.Min(size.X, size.Y) * 0.5f);

        // Inner Cross (3 Quads)
        FillRect(new Vector2(position.X + radius, position.Y),                   new Vector2(size.X - radius * 2f, size.Y), color);
        FillRect(new Vector2(position.X, position.Y + radius),                   new Vector2(radius, size.Y - radius * 2f), color);
        FillRect(new Vector2(position.X + size.X - radius, position.Y + radius), new Vector2(radius, size.Y - radius * 2f), color);

        // 4 Corner Arcs
        Vector2 tl = position + new Vector2(radius,          radius);
        Vector2 tr = position + new Vector2(size.X - radius, radius);
        Vector2 br = position + new Vector2(size.X - radius, size.Y - radius);
        Vector2 bl = position + new Vector2(radius,          size.Y - radius);

        FillCornerArc(tl, radius, ArcCorner.TopLeft,     color, segments);
        FillCornerArc(tr, radius, ArcCorner.TopRight,    color, segments);
        FillCornerArc(br, radius, ArcCorner.BottomRight, color, segments);
        FillCornerArc(bl, radius, ArcCorner.BottomLeft,  color, segments);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillCornerArc(Vector2 center, float radius, ArcCorner corner, Color32 color, int segments)
    {
        if (segments < 1) segments = 1;

        if (segments <= ArcLookups.CornerTableLength)
        {
            ReadOnlySpan<Vector2> lookup = ArcLookups.CornerTables[segments];
            ArcLookups.GetCornerTransform(corner, out float signX, out float signY, out bool swapXY);

            for (int i = 0; i < segments; i += 2)
            {
                Vector2 u0 = lookup[i];
                Vector2 u1 = lookup[i + 1];
                Vector2 u2 = (i + 2 <= segments) ? lookup[i + 2] : u1;

                Vector2 dir0 = swapXY ? new Vector2(u0.Y * signX, u0.X * signY) : new Vector2(u0.X * signX, u0.Y * signY);
                Vector2 dir1 = swapXY ? new Vector2(u1.Y * signX, u1.X * signY) : new Vector2(u1.X * signX, u1.Y * signY);
                Vector2 dir2 = swapXY ? new Vector2(u2.Y * signX, u2.X * signY) : new Vector2(u2.X * signX, u2.Y * signY);

                FillQuad(center, center + dir0 * radius, center + dir1 * radius, center + dir2 * radius, color);
            }
            return;
        }

        var (startAngle, endAngle) = ArcLookups.GetCornerAngles(corner);
        FillArc(center, radius, startAngle, endAngle, color, segments);
    }

    public void FillArc(Vector2 center, float radius, float startAngle, float endAngle, Color32 color, int segments)
    {
        if (color.A == 0) return;
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

    /// <summary>
    /// Draws a filled circle (1 quad renders 2 pie-slices using the quad index pattern).
    /// </summary>
    public void FillCircle(Vector2 center, float radius, Color32 color, int segments = 32)
    {
        if (color.A == 0) return;
        if (segments < 3) segments = 3;
        float step = MathF.PI * 2f / segments;

        for (int i = 0; i < segments; i += 2)
        {
            float a0 = i * step;
            float a1 = (i + 1) * step;
            float a2 = (i + 2) * step;

            Vector2 p0 = center + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * radius;
            Vector2 p1 = center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius;
            Vector2 p2 = (i + 2 <= segments)
                ? center + new Vector2(MathF.Cos(a2), MathF.Sin(a2)) * radius
                : p1;

            // Quad layout: (Center, P0, P1, P2) maps to 2 triangles: (Center, P0, P1) & (P1, P2, Center)
            FillQuad(center, p0, p1, p2, color);
        }
    }

    /// <summary>
    /// Draws an un-filled circle outline.
    /// </summary>
    public void StrokeCircle(Vector2 center, float radius, float thickness, Color32 color, int segments = 32)
    {
        if (color.A == 0) return;
        if (segments < 3) segments = 3;
        float step = MathF.PI * 2f / segments;
        float halfThick = thickness * 0.5f;
        float rInner = radius - halfThick;
        float rOuter = radius + halfThick;

        for (int i = 0; i < segments; i++)
        {
            float a0 = i * step;
            float a1 = (i + 1) * step;

            Vector2 dir0 = new Vector2(MathF.Cos(a0), MathF.Sin(a0));
            Vector2 dir1 = new Vector2(MathF.Cos(a1), MathF.Sin(a1));

            FillQuad(
                center + dir0 * rInner, // Inner Start
                center + dir0 * rOuter, // Outer Start
                center + dir1 * rOuter, // Outer End
                center + dir1 * rInner, // Inner End
                color
            );
        }
    }
}

