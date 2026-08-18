// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

// ReSharper disable UseWithExpressionToCopyStruct
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable MergeIntoPattern
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable RedundantArgumentDefaultValue
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public readonly ref struct Draw2D : IDisposable
{
    internal readonly   Batch2D     batch;  //  8 bytes
    private  readonly   RenderPass  pass;   //  8 bytes

    public              Font        DefaultFont => batch.defaultFont;
    
    internal Draw2D(Batch2D batch, RenderPass pass)
    {
        this.batch  = batch;
        this.pass   = pass;
    }

    
    
#region Sprites
    /// <summary>
    /// Draws a sprite using normal 0..1 UV coordinates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(in Vector2 position, in Vector2 size, GpuTextureView texture, Color32 color = default, in Vector2 uvMin = default, Vector2 uvMax = default)
    {
        if (color.Packed == 0) color = Color32.White;
        if (uvMax == default) uvMax = new Vector2(1f, 1f);
        DrawQuad(position, size, uvMin, uvMax, color, new ImTextureView(texture));
    }

    /// <summary>
    /// Draws a rotated sprite with pivot (0..1 normalized).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(in Vector2 position, in Vector2 size, float rotation, in Vector2 pivot, GpuTextureView texture, Color32 color = default, in Vector2 uvMin = default, Vector2 uvMax = default)
    {
        if (color.Packed == 0) color = Color32.White;
        if (uvMax == default) uvMax = new Vector2(1f, 1f);
        DrawQuadRotated(position, size, rotation, pivot, uvMin, uvMax, color, new ImTextureView(texture));
    }

    /// <summary>
    /// Draws a sub-region (source rect in pixels) from a texture/spritesheet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(in Vector2 position, in Vector2 size, GpuTextureView texture, in Vector2 sourceRectPos, in Vector2 sourceRectSize, in Vector2 textureSize, Color32 color = default)
    {
        if (color.Packed == 0) color = Color32.White;
        Vector2 uvMin = sourceRectPos / textureSize;
        Vector2 uvMax = (sourceRectPos + sourceRectSize) / textureSize;
        DrawQuad(position, size, uvMin, uvMax, color, new ImTextureView(texture));
    }

    /// <summary>
    /// Draws a rotated sub-region from a texture with pivot (0..1 normalized).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(in Vector2 position, in Vector2 size, float rotation, in Vector2 pivot, GpuTextureView texture, in Vector2 sourceRectPos, in Vector2 sourceRectSize, in Vector2 textureSize, Color32 color = default)
    {
        if (color.Packed == 0) color = Color32.White;
        Vector2 uvMin = sourceRectPos / textureSize;
        Vector2 uvMax = (sourceRectPos + sourceRectSize) / textureSize;
        DrawQuadRotated(position, size, rotation, pivot, uvMin, uvMax, color, new ImTextureView(texture));
    }

    /// <summary>
    /// Draws a 9-slice sprite using the full texture where borders and center are tiled (repeated).
    /// borderThickness: (Left, Top, Right, Bottom) in pixels.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite9SliceTiled(
        in Vector2          position, 
        in Vector2          size, 
        in GpuTextureView   texture,
        in Vector4          borderThickness, 
        in Vector2          textureSize, 
           Color32          color = default)
    {
        DrawSprite9SliceTiled(position, size, texture, Vector2.Zero, textureSize, textureSize, borderThickness, color);
    }

    /// <summary>
    /// Draws a 9-slice sprite from a sub-region (Spritesheet/Atlas) where borders and center are tiled (repeated).
    /// borderThickness: (Left, Top, Right, Bottom) in pixels.
    /// </summary>
    public void DrawSprite9SliceTiled(
        in Vector2          position, 
        in Vector2          size, 
        in GpuTextureView   texture, 
        in Vector2          sourceRectPos, 
        in Vector2          sourceRectSize, 
        in Vector2          textureSize, 
        in Vector4          borderThickness, 
           Color32          color = default)
    {
        if (color.Packed == 0) color = Color32.White;

        float L = borderThickness.X;
        float T = borderThickness.Y;
        float R = borderThickness.Z;
        float B = borderThickness.W;

        float srcInnerW = sourceRectSize.X - L - R;
        float srcInnerH = sourceRectSize.Y - T - B;

        float destInnerW = size.X - L - R;
        float destInnerH = size.Y - T - B;

        // prevent division by 0 of invalid values
        if (srcInnerW <= 0f || srcInnerH <= 0f) return;

        // UV-coordinates of 3x3 grid
        Vector2 u0 = sourceRectPos / textureSize;
        Vector2 u1 = (sourceRectPos + new Vector2(L, T)) / textureSize;
        Vector2 u2 = (sourceRectPos + sourceRectSize - new Vector2(R, B)) / textureSize;
        Vector2 u3 = (sourceRectPos + sourceRectSize) / textureSize;

        // --- 4 corners (fixed size) ---
        var texView = new ImTextureView(texture);
        DrawQuad(position, new Vector2(L, T), u0, u1, color, texView);
        DrawQuad(new Vector2(position.X + size.X - R, position.Y), new Vector2(R, T), new Vector2(u2.X, u0.Y), new Vector2(u3.X, u1.Y), color, texView);
        DrawQuad(new Vector2(position.X, position.Y + size.Y - B), new Vector2(L, B), new Vector2(u0.X, u2.Y), new Vector2(u1.X, u3.Y), color, texView);
        DrawQuad(new Vector2(position.X + size.X - R, position.Y + size.Y - B), new Vector2(R, B), u2, u3, color, texView);

        // --- top bottom border (Horizontal tiled) ---
        for (float x = 0; x < destInnerW; x += srcInnerW)
        {
            float drawW = MathF.Min(srcInnerW, destInnerW - x);
            float uMaxX = u1.X + (u2.X - u1.X) * (drawW / srcInnerW);

            DrawQuad(new Vector2(position.X + L + x, position.Y), new Vector2(drawW, T), new Vector2(u1.X, u0.Y), new Vector2(uMaxX, u1.Y), color, texView);
            DrawQuad(new Vector2(position.X + L + x, position.Y + size.Y - B), new Vector2(drawW, B), new Vector2(u1.X, u2.Y), new Vector2(uMaxX, u3.Y), color, texView);
        }

        // --- left / right border (vertical tiled) ---
        for (float y = 0; y < destInnerH; y += srcInnerH)
        {
            float drawH = MathF.Min(srcInnerH, destInnerH - y);
            float uMaxY = u1.Y + (u2.Y - u1.Y) * (drawH / srcInnerH);

            DrawQuad(new Vector2(position.X, position.Y + T + y), new Vector2(L, drawH), new Vector2(u0.X, u1.Y), new Vector2(u1.X, uMaxY), color, texView);
            DrawQuad(new Vector2(position.X + size.X - R, position.Y + T + y), new Vector2(R, drawH), new Vector2(u2.X, u1.Y), new Vector2(u3.X, uMaxY), color, texView);
        }

        // --- inner area (2D-grid tiled) ---
        for (float x = 0; x < destInnerW; x += srcInnerW)
        {
            float drawW = MathF.Min(srcInnerW, destInnerW - x);
            float uMaxX = u1.X + (u2.X - u1.X) * (drawW / srcInnerW);

            for (float y = 0; y < destInnerH; y += srcInnerH)
            {
                float drawH = MathF.Min(srcInnerH, destInnerH - y);
                float uMaxY = u1.Y + (u2.Y - u1.Y) * (drawH / srcInnerH);

                DrawQuad(new Vector2(position.X + L + x, position.Y + T + y), new Vector2(drawW, drawH), u1, new Vector2(uMaxX, uMaxY), color, texView);
            }
        }
    }
#endregion



#region Quads (private)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawQuad(in Vector2 position, in Vector2 size, in Vector2 uvMin, in Vector2 uvMax, Color32 color, ImTextureView texture)
    {
        var bat = batch;
        if (bat.vertexCount + 4 > bat.vertexBuffer.Length || bat.currentTexture.Handle != texture.Handle) {
            Flush();
        }
        bat.currentTexture = texture;

        var span = bat.vertexBuffer.InOut(bat.vertexCount, 4).Span;
        
        float x1 = position.X;
        float y1 = position.Y;
        float x2 = position.X + size.X;
        float y2 = position.Y + size.Y;

        span[0] = new Vertex2D { position = new Vector2(x1, y1), uv = uvMin,                            color = color.Packed }; // Top-Left
        span[1] = new Vertex2D { position = new Vector2(x2, y1), uv = new Vector2(uvMax.X, uvMin.Y),    color = color.Packed }; // Top-Right
        span[2] = new Vertex2D { position = new Vector2(x2, y2), uv = uvMax,                            color = color.Packed }; // Bottom-Right
        span[3] = new Vertex2D { position = new Vector2(x1, y2), uv = new Vector2(uvMin.X, uvMax.Y),    color = color.Packed }; // Bottom-Left

        bat.vertexCount += 4;
    }

    /// <summary>
    /// Draws a rotated quad transform around a normalized pivot (0..1).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawQuadRotated(in Vector2 position, in Vector2 size, float rotation, in Vector2 pivot, in Vector2 uvMin, in Vector2 uvMax, Color32 color, ImTextureView texture)
    {
        if (rotation == 0f) {
            DrawQuad(position - (pivot * size), size, uvMin, uvMax, color, texture);
            return;
        }
        var bat = batch;
        if (bat.vertexCount + 4 > bat.vertexBuffer.Length || bat.currentTexture.Handle != texture.Handle) {
            Flush();
        }
        bat.currentTexture = texture;

        var span = bat.vertexBuffer.InOut(bat.vertexCount, 4).Span;

        float cos = MathF.Cos(rotation);
        float sin = MathF.Sin(rotation);

        // Local offsets relative to pivot
        float l = -pivot.X * size.X;
        float r = (1f - pivot.X) * size.X;
        float t = -pivot.Y * size.Y;
        float b = (1f - pivot.Y) * size.Y;

        // [0] Top-Left  [1] Top-Right  [2] Bottom-Right  [3] Bottom-Left  
        span[0] = new Vertex2D { position = new Vector2(position.X + l * cos - t * sin, position.Y + l * sin + t * cos), uv = uvMin,                            color = color.Packed };
        span[1] = new Vertex2D { position = new Vector2(position.X + r * cos - t * sin, position.Y + r * sin + t * cos), uv = new Vector2(uvMax.X, uvMin.Y),    color = color.Packed };
        span[2] = new Vertex2D { position = new Vector2(position.X + r * cos - b * sin, position.Y + r * sin + b * cos), uv = uvMax,                            color = color.Packed };
        span[3] = new Vertex2D { position = new Vector2(position.X + l * cos - b * sin, position.Y + l * sin + b * cos), uv = new Vector2(uvMin.X, uvMax.Y),    color = color.Packed };

        bat.vertexCount += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawQuadSolid(in Vector2 v0, in Vector2 v1, in Vector2 v2, in Vector2 v3, Color32 color)
    {
        var bat = batch;
        if (bat.vertexCount + 4 > bat.vertexBuffer.Length || !bat.currentTexture.hasWhitePixel) {
            Flush();
            bat.currentTexture = bat.defaultFontTexture;
        }
        var span = bat.vertexBuffer.InOut(bat.vertexCount, 4).Span;
        var uv = bat.currentTexture.whiteUv;

        span[0] = new Vertex2D { position = v0, uv = uv, color = color.Packed };
        span[1] = new Vertex2D { position = v1, uv = uv, color = color.Packed };
        span[2] = new Vertex2D { position = v2, uv = uv, color = color.Packed };
        span[3] = new Vertex2D { position = v3, uv = uv, color = color.Packed };

        bat.vertexCount += 4;
    }
#endregion



#region Primitives
    public void Rectangle(in Vector2 position, in Vector2 size, Color32 color)
    {
        var bat = batch;
        if (bat.currentTexture.hasWhitePixel) {
            DrawQuad(position, size, bat.currentTexture.whiteUv, bat.currentTexture.whiteUv, color, bat.currentTexture);
        } else {
            DrawQuad(position, size, bat.defaultFontTexture.whiteUv, bat.defaultFontTexture.whiteUv, color, bat.defaultFontTexture);
        }
    }

    /// <summary>
    /// Draws a rectangle with per-corner gradient colors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RectangleGradient(in Vector2 position, in Vector2 size, Color32 topLeft, Color32 topRight, Color32 bottomRight, Color32 bottomLeft)
    {
        var bat = batch;
        var texView = bat.currentTexture.hasWhitePixel ? bat.currentTexture : bat.defaultFontTexture;
        if (bat.vertexCount + 4 > bat.vertexBuffer.Length || bat.currentTexture.Handle != texView.Handle) {
            Flush();
        }
        bat.currentTexture = texView;
        var uv = bat.currentTexture.whiteUv;

        var span = bat.vertexBuffer.InOut(bat.vertexCount, 4).Span;

        float x1 = position.X;
        float y1 = position.Y;
        float x2 = position.X + size.X;
        float y2 = position.Y + size.Y;

        span[0] = new Vertex2D { position = new Vector2(x1, y1), uv = uv, color = topLeft.Packed };
        span[1] = new Vertex2D { position = new Vector2(x2, y1), uv = uv, color = topRight.Packed };
        span[2] = new Vertex2D { position = new Vector2(x2, y2), uv = uv, color = bottomRight.Packed };
        span[3] = new Vertex2D { position = new Vector2(x1, y2), uv = uv, color = bottomLeft.Packed };

        bat.vertexCount += 4;
    }

    /// <summary>
    /// Draws a vertical gradient rectangle (top to bottom).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RectangleGradientVertical(in Vector2 position, in Vector2 size, Color32 top, Color32 bottom)
        => RectangleGradient(position, size, top, top, bottom, bottom);



    /// <summary>
    /// Draws a single filled triangle using a quad slot (duplicates 3rd vertex).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Triangle(in Vector2 v0, in Vector2 v1, in Vector2 v2, Color32 color)
    {
        DrawQuadSolid(v0, v1, v2, v2, color);
    }

    /// <summary>
    /// Draws a thick line segment between two points.
    /// </summary>
    public void Line(in Vector2 start, in Vector2 end, float thickness, Color32 color)
    {
        Vector2 dir = end - start;
        float len = dir.Length();
        if (len < 0.0001f) return;

        // Normal vector perpendicular to the line
        Vector2 normal = new Vector2(-dir.Y, dir.X) / len * (thickness * 0.5f);

        DrawQuadSolid(
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
    public void RectangleLines(in Vector2 position, in Vector2 size, float thickness, Color32 color)
    {
        float x = position.X;
        float y = position.Y;
        float w = size.X;
        float h = size.Y;

        // Top, Right, Bottom, Left edges
        Rectangle(new Vector2(x, y), new Vector2(w, thickness), color);
        Rectangle(new Vector2(x + w - thickness, y + thickness), new Vector2(thickness, h - thickness * 2f), color);
        Rectangle(new Vector2(x, y + h - thickness), new Vector2(w, thickness), color);
        Rectangle(new Vector2(x, y + thickness), new Vector2(thickness, h - thickness * 2f), color);
    }

    /// <summary>
    /// Draws a filled rounded rectangle.
    /// </summary>
    public void RectangleRounded(in Vector2 position, in Vector2 size, float cornerRadius, Color32 color, int cornerSegments = 8)
    {
        if (cornerRadius <= 0f) {
            Rectangle(position, size, color);
            return;
        }

        cornerRadius = MathF.Min(cornerRadius, MathF.Min(size.X, size.Y) * 0.5f);

        // Inner Cross (3 Quads)
        Rectangle(new Vector2(position.X + cornerRadius, position.Y), new Vector2(size.X - cornerRadius * 2f, size.Y), color);
        Rectangle(new Vector2(position.X, position.Y + cornerRadius), new Vector2(cornerRadius, size.Y - cornerRadius * 2f), color);
        Rectangle(new Vector2(position.X + size.X - cornerRadius, position.Y + cornerRadius), new Vector2(cornerRadius, size.Y - cornerRadius * 2f), color);

        // 4 Corner Arcs
        Vector2 tl = position + new Vector2(cornerRadius, cornerRadius);
        Vector2 tr = position + new Vector2(size.X - cornerRadius, cornerRadius);
        Vector2 br = position + new Vector2(size.X - cornerRadius, size.Y - cornerRadius);
        Vector2 bl = position + new Vector2(cornerRadius, size.Y - cornerRadius);

        DrawCornerArc(tl, cornerRadius, MathF.PI, MathF.PI * 1.5f, color, cornerSegments);
        DrawCornerArc(tr, cornerRadius, MathF.PI * 1.5f, MathF.PI * 2f, color, cornerSegments);
        DrawCornerArc(br, cornerRadius, 0f, MathF.PI * 0.5f, color, cornerSegments);
        DrawCornerArc(bl, cornerRadius, MathF.PI * 0.5f, MathF.PI, color, cornerSegments);
    }

    private void DrawCornerArc(in Vector2 center, float radius, float startAngle, float endAngle, Color32 color, int segments)
    {
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

            DrawQuadSolid(center, p0, p1, p2, color);
        }
    }

    /// <summary>
    /// Draws a filled circle (1 quad renders 2 pie-slices using the quad index pattern).
    /// </summary>
    public void Circle(in Vector2 center, float radius, Color32 color, int segments = 32)
    {
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
            DrawQuadSolid(center, p0, p1, p2, color);
        }
    }

    /// <summary>
    /// Draws an un-filled circle outline.
    /// </summary>
    public void CircleLines(in Vector2 center, float radius, float thickness, Color32 color, int segments = 32)
    {
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

            DrawQuadSolid(
                center + dir0 * rInner, // Inner Start
                center + dir0 * rOuter, // Outer Start
                center + dir1 * rOuter, // Outer End
                center + dir1 * rInner, // Inner End
                color
            );
        }
    }
#endregion



#region Text
    /// <summary>
    /// Draws a text string using a bitmap font atlas.
    /// </summary>
    public Vector2 DrawString(ReadOnlySpan<char> text, Vector2 position, Color32 color, Font? font = null, float scale = 1.0f)
    {
        font ??= batch.defaultFont;

        Vector2 currentPos = position;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            // Ignore carriage return (\r\n Windows line endings)
            if (c == '\r') {
                continue;
            }
            // Handle line breaks
            if (c == '\n') {
                currentPos.X = position.X;
                currentPos.Y += font.lineHeight * scale;
                continue;
            }
            if (!font.TryGetGlyph(c, out var glyph)) {
                // Fallback for missing characters
                if (!font.TryGetGlyph('?', out glyph)) continue;
            }
            // Render glyph if it has visible dimensions (skips spaces)
            if (glyph.sourceSize.X > 0f && glyph.sourceSize.Y > 0f) {
                Vector2 renderPos = currentPos + (glyph.offset * scale);
                Vector2 renderSize = glyph.sourceSize * scale;
                DrawSprite(renderPos, renderSize, font.textureView.native, glyph.sourcePos, glyph.sourceSize, font.textureSize, color);
            }
            currentPos.X += glyph.advance * scale;
        }
        return new Vector2(currentPos.X - position.X, font.lineHeight * scale);
    }
    
    /// <summary>
    /// Calculates the bounding box size (width and height) of a text string in pixels.
    /// </summary>
    public Vector2 MeasureString(ReadOnlySpan<char> text, Font? font = null, float scale = 1.0f)
    {
        font ??= batch.defaultFont;

        float maxWidth = 0f;
        float currentLineWidth = 0f;
        int lineCount = 1;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r') {
                continue;
            }
            if (c == '\n') {
                maxWidth = MathF.Max(maxWidth, currentLineWidth);
                currentLineWidth = 0f;
                lineCount++;
                continue;
            }
            if (!font.TryGetGlyph(c, out var glyph)) {
                if (!font.TryGetGlyph('?', out glyph)) continue;
            }
            currentLineWidth += glyph.advance * scale;
        }
        maxWidth = MathF.Max(maxWidth, currentLineWidth);
        
        return new Vector2(maxWidth, lineCount * font.lineHeight * scale);
    }
    
    /// <summary>
    /// Draws text aligned relative to a bounding position or box.
    /// </summary>
    public void DrawStringAligned(ReadOnlySpan<char> text, Vector2 position, TextAlignment alignment, Color32 color, Font? font = null, float scale = 1.0f)
    {
        if (alignment == TextAlignment.Left)
        {
            DrawString(text, position, color, font, scale);
            return;
        }

        Vector2 size = MeasureString(text, font, scale);
        Vector2 alignedPos = position;

        if (alignment == TextAlignment.Center)
            alignedPos.X -= size.X * 0.5f;
        else if (alignment == TextAlignment.Right)
            alignedPos.X -= size.X;

        DrawString(text, alignedPos, color, font, scale);
    }
    
    /// <summary>
    /// Draws text aligned horizontally and vertically within a target bounding rectangle.
    /// Supports multi-line text and optional word wrapping.
    /// </summary>
    public void DrawStringInRect(
        ReadOnlySpan<char>  text, 
        Vector2             position, 
        Vector2             size, 
        TextAlignment       horizontalAlignment, 
        VerticalAlignment   verticalAlignment, 
        Color32             color, 
        Font?               font = null, 
        bool                wordWrap = false,
        float               scale = 1.0f)
    {
        if (text.IsEmpty || size.X <= 0f || size.Y <= 0f) return;
        font ??= batch.defaultFont;

        float effectiveMaxWidth = wordWrap ? size.X : float.MaxValue;

        // Pass 1: Count lines to calculate total block height
        int lineCount = 0;
        foreach (ReadOnlySpan<char> _ in GetWrappedLines(text, effectiveMaxWidth, font, scale)) {
            lineCount++;
        }

        if (lineCount == 0) return;

        float totalHeight = lineCount * font.lineHeight * scale;

        // Calculate vertical starting Y position
        float startY = verticalAlignment switch
        {
            VerticalAlignment.Middle => position.Y + (size.Y - totalHeight) * 0.5f,
            VerticalAlignment.Bottom => position.Y + size.Y - totalHeight,
            _                        => position.Y // Top
        };

        // Pass 2: Draw each line horizontally aligned
        float currentY = startY;

        foreach (ReadOnlySpan<char> line in GetWrappedLines(text, effectiveMaxWidth, font, scale))
        {
            float lineX = position.X;

            if (horizontalAlignment != TextAlignment.Left)
            {
                float lineWidth = MeasureString(line, font, scale).X;

                if (horizontalAlignment == TextAlignment.Center)
                    lineX += (size.X - lineWidth) * 0.5f;
                else if (horizontalAlignment == TextAlignment.Right)
                    lineX += size.X - lineWidth;
            }

            DrawString(line, new Vector2(lineX, currentY), color, font, scale);
            currentY += font.lineHeight * scale;
        }
    }
    
    /// <summary>
    /// Truncates a string to fit within a maximum pixel width and appends '...'.
    /// Allocates a new string.
    /// </summary>
    public string TruncateWithEllipsis(ReadOnlySpan<char> text, float maxWidth, Font? font = null, float scale = 1.0f)
    {
        font ??= batch.defaultFont;
        int visibleLength = GetVisibleLengthWithEllipsis(text, maxWidth, font, scale);

        if (visibleLength >= text.Length)
            return text.ToString();

        return string.Concat(text[..visibleLength], "...");
    }

    /// <summary>
    /// Draws text at position, automatically truncating with '...' if it exceeds maxWidth.
    /// </summary>
    public void DrawStringTruncated(ReadOnlySpan<char> text, Vector2 position, float maxWidth, Color32 color, Font? font = null, float scale = 1.0f)
    {
        font ??= batch.defaultFont;
        int visibleLength = GetVisibleLengthWithEllipsis(text, maxWidth, font, scale);

        // If whole text fits, render normally
        if (visibleLength >= text.Length)
        {
            DrawString(text, position, color, font, scale);
            return;
        }

        // Render visible substring directly without GC allocations
        DrawString(text[..visibleLength], position, color, font, scale);

        // Calculate position for '...' and render it
        Vector2 ellipsisPos = position;
        for (int i = 0; i < visibleLength; i++)
        {
            if (font.TryGetGlyph(text[i], out var glyph))
                ellipsisPos.X += glyph.advance * scale;
        }

        DrawString("...", ellipsisPos, color, font, scale);
    }

    /// <summary>
    /// Internal core logic: Determines how many characters fit before '...' must be appended.
    /// </summary>
    private static int GetVisibleLengthWithEllipsis(ReadOnlySpan<char> text, float maxWidth, Font font, float scale = 1.0f)
    {
        if (!font.TryGetGlyph('.', out var dotGlyph))
            return text.Length;

        float ellipsisWidth = dotGlyph.advance * scale * 3f;
        float currentWidth = ellipsisWidth;
        int visibleLength = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r' || c == '\n') break;
            if (!font.TryGetGlyph(c, out var glyph)) continue;

            float advance = glyph.advance * scale;
            if (currentWidth + advance > maxWidth) break;

            currentWidth += advance;
            visibleLength++;
        }

        return visibleLength;
    }
    
    /// <summary>
    /// Helper method to create the line enumerator.
    /// </summary>
    public WrappedLineEnumerator GetWrappedLines(ReadOnlySpan<char> text, float maxWidth, Font? font = null, float scale = 1.0f)
    {
        font ??= batch.defaultFont;
        return new WrappedLineEnumerator(text, maxWidth, font, scale);
    }

    /// <summary>
    /// Wraps text by inserting line breaks ('\n'). Allocates a new string.
    /// </summary>
    public string WrapText(ReadOnlySpan<char> text, float maxWidth, Font? font = null, float scale = 1.0f)
    {
        if (text.IsEmpty || maxWidth <= 0f) return string.Empty;

        var sb = new StringBuilder(text.Length);

        foreach (ReadOnlySpan<char> line in GetWrappedLines(text, maxWidth, font, scale)) {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(line);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Draws word-wrapped text directly onto the screen.
    /// </summary>
    public int DrawStringWrapped(ReadOnlySpan<char> text, Vector2 position, float maxWidth, Color32 color, Font? font = null, float scale = 1.0f)
    {
        if (text.IsEmpty || maxWidth <= 0f) return 0;
        font ??= batch.defaultFont;

        Vector2 currentPos = position;
        int lineCount = 0;

        foreach (ReadOnlySpan<char> line in GetWrappedLines(text, maxWidth, font, scale))
        {
            DrawString(line, currentPos, color, font, scale);
            currentPos.Y += font.lineHeight * scale;
            lineCount++;
        }
        return lineCount;
    }
#endregion



#region State / Pipeline
    public ScissorScope PushScissor(Vector2 position, Vector2 size)
    {
        var scissorStack = batch.scissorStack;
        var cur = scissorStack.Count > 0 ? scissorStack.Peek() : new RectVector2(Vector2.Zero, batch.viewport);

        var intersectPos    = Vector2.Max(cur.pos, position);
        var curMax          = cur.pos + cur.size;
        var newMax          = position + size;
        var intersectMax    = Vector2.Min(curMax, newMax);
        var intersectSize   = Vector2.Max(Vector2.Zero, intersectMax - intersectPos);

        var scissor = new RectVector2(intersectPos, intersectSize);
        scissorStack.Push(scissor);

        Flush();
        batch.currentScissor = scissor;
        return new ScissorScope(this);
    }

    public void PopScissor()
    {
        var scissorStack = batch.scissorStack;
        if (scissorStack.Count > 0) {
            scissorStack.Pop();
        }
        var scissor = scissorStack.Count > 0 ? scissorStack.Peek() : new RectVector2(Vector2.Zero, batch.viewport);
        Flush();
        batch.currentScissor = scissor;
    }
    
    public void SetFilterMode(FilterMode filterMode)
    {
        var bat = batch;
        var targetSampler = filterMode == FilterMode.Nearest ? bat.samplerNearest : bat.samplerLinear;

        if (bat.currentSampler == targetSampler) return;

        Flush();
        bat.currentSampler = targetSampler;
    }

    public void SetViewport(float width, float height)
    {
        Flush();

        var bat = batch;
        bat.viewport = new Vector2(width, height);
        
        // base projection for window size
        bat.defaultOrtho = Matrix4x4.CreateOrthographicOffCenter(0f, width, height, 0f, -1f, 1f);
        
        // combine with current camera transform
        bat.uniforms.projection = bat.currentTransform * bat.defaultOrtho;
    }

    public TransformScope PushTransform(in Matrix4x4 transform)
    {
        var transformStack = batch.transformStack;
        var parent    = transformStack.Count > 0 ? transformStack.Peek() : Matrix4x4.Identity;
        var combined  = transform * parent;

        transformStack.Push(combined);
        ApplyTransform(combined);
        return new TransformScope(this);
    }

    public void PopTransform()
    {
        var transformStack = batch.transformStack;
        if (transformStack.Count > 0) {
            transformStack.Pop();
        }
        var targetTransform = transformStack.Count > 0 ? transformStack.Peek() : Matrix4x4.Identity;

        ApplyTransform(targetTransform);
    }

    private void ApplyTransform(in Matrix4x4 transform)
    {
        var bat = batch;
        if (bat.currentTransform == transform) return;

        Flush();

        bat.currentTransform    = transform;
        bat.uniforms.projection = bat.currentTransform * bat.defaultOrtho;
    }

    public void SetBlendState(BlendState blendState)
    {
        if (blendState == batch.currentBlendState) return;
        
        Flush();
        batch.currentBlendState = blendState;
    }
    
    public ZIndexScope PushZIndex(int zIndex)
    {
        var bat = batch;
        bat.zIndexStack.Push(bat.currentZIndex);

        Flush();
        bat.currentZIndex = zIndex;
        bat.sortZIndex    = true;
        return new ZIndexScope(this);
    }

    public void PopZIndex()
    {
        var bat = batch;
        if (bat.zIndexStack.Count == 0) return;

        int prevZIndex = bat.zIndexStack.Pop();

        Flush();
        bat.currentZIndex = prevZIndex;
    }

    public void Flush()
    {
        var bat = batch;
        int pendingVertices = bat.vertexCount - bat.vertexStart;
        if (pendingVertices <= 0) {
            return;
        }

        int pendingQuads = pendingVertices / 4;

        var texture     = bat.currentTexture;
        var vertexView  = bat.vertexBuffer.InOut(bat.vertexStart, pendingVertices);
        var indexView   = bat.indexBuffer.In(0, pendingQuads * 6);
        var config      = bat.renderConfigs[(int)bat.currentBlendState];
        bat.vertexStart = bat.vertexCount;

        // Batch2D.Draw(pass, config, bat.uniforms, texture, bat.currentSampler, vertexView, indexView);
        
        bat.drawCommands.Add(new DrawCommand {
            zIndex      = bat.currentZIndex,
            sequence    = bat.currentSequence++, 
            texture     = texture.native,
            vertexView  = vertexView,
            indexView   = indexView,
            config      = config,
            uniforms    = bat.uniforms,
            sampler     = bat.currentSampler,
            scissor     = bat.currentScissor,
        });
    }
    
    public void Dispose()
    {
        if (pass.IsDisposed) {
            return;
        }
        Flush();
        var bat = batch;
        if (bat.vertexCount > 0)
        {
            // Upload vertexBuffer with a single wgpu call
            bat.vertexBuffer.InOut(0, bat.vertexCount).Write();

            var commands = bat.drawCommands;
            var segments = bat.commandSegments;
            segments.Clear();
            if (bat.sortZIndex) {
                SortCommands(commands, segments);
            } else {
                segments.Add(new CmdSegment { index = 0, length = commands.Count });
            }
            var scissor = new RectVector2(Vector2.Zero, bat.viewport);

            foreach (var segment in segments)
            {
                for (int n = 0; n < segment.length; n++)
                {
                    var cmd = commands[segment.index + n];
                    if (!cmd.scissor.Equals(scissor)) {
                        scissor = cmd.scissor;
                        pass.SetScissorRect((int)scissor.pos.X, (int)scissor.pos.Y, (int)scissor.size.X, (int)scissor.size.Y);    
                    }
                    Batch2D.Draw(pass, cmd.config, cmd.uniforms, cmd.texture, cmd.sampler, cmd.vertexView, cmd.indexView);
                }
            }

        }
        pass.Dispose();
    }
    
    private static void SortCommands(List<DrawCommand> commands, List<CmdSegment> segments)
    {
        // commands.Sort((a, b) => (a.zIndex, a.sequence).CompareTo((b.zIndex, b.sequence)));
        
        // Run-Length optimization - of commented Sort() above
        var command_0   = commands[0];
        int zIndex      = command_0.zIndex;
        var segment     = new CmdSegment { zIndex = zIndex, sequence = command_0.sequence, index = 0, length = 1 };
        
        for (int n = 1; n < commands.Count; n++)
        {
            var cmd = commands[n];
            if (zIndex == cmd.zIndex) {
                segment.length++;
                continue;
            }
            segments.Add(segment);
            zIndex              = cmd.zIndex;
            segment.zIndex      = zIndex;
            segment.sequence    = cmd.sequence;
            segment.index       = n;
            segment.length      = 1;
        }
        segments.Add(segment);
        
        segments.Sort((a, b) => (a.zIndex, a.sequence).CompareTo((b.zIndex, b.sequence)));
    }
    
    public DrawGui BeginGui() => new(this, batch);
#endregion
}

