// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.WebGPU;
using Shaders.Imdraw;

// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable MergeIntoPattern
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable RedundantArgumentDefaultValue
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public enum TextAlignment
{
    Left,
    Center,
    Right
}

public ref struct Draw2D : IDisposable
{
    private readonly Batch2D    batch;
    private          RenderPass pass;

    
    public void Dispose() {
        Flush();
        pass.Dispose();
    }
    
    internal Draw2D(Batch2D batch, RenderPass pass)
    {
        this.batch  = batch;
        this.pass   = pass;
    }
    
#region Quads / Sprites

    public void Rectangle(in Vector2 position, in Vector2 size, Color32 color)
    {
        DrawQuad(position, size, new Vector2(0f, 0f), new Vector2(1f, 1f), color, null);
    }
    
    /// <summary>
    /// Draws a sprite using normal 0..1 UV coordinates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(in Vector2 position, in Vector2 size, GpuTextureView texture, Color32 color = default, in Vector2 uvMin = default, Vector2 uvMax = default)
    {
        if (color.Packed == 0) color = Color32.White;
        if (uvMax == default) uvMax = new Vector2(1f, 1f);
        DrawQuad(position, size, uvMin, uvMax, color, texture);
    }

    /// <summary>
    /// Draws a rotated sprite with pivot (0..1 normalized).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(in Vector2 position, in Vector2 size, float rotation, in Vector2 pivot, GpuTextureView texture, Color32 color = default, in Vector2 uvMin = default, Vector2 uvMax = default)
    {
        if (color.Packed == 0) color = Color32.White;
        if (uvMax == default) uvMax = new Vector2(1f, 1f);
        DrawQuadRotated(position, size, rotation, pivot, uvMin, uvMax, color, texture);
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
        DrawQuad(position, size, uvMin, uvMax, color, texture);
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
        DrawQuadRotated(position, size, rotation, pivot, uvMin, uvMax, color, texture);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawQuad(in Vector2 position, in Vector2 size, in Vector2 uvMin, in Vector2 uvMax, Color32 color, GpuTextureView? texture = null)
    {
        var bat = batch;
        var texView = texture ?? bat.defaultWhiteTextureView;
        
        // flush if full (4 vertices per quad) or texture changed
        if (bat.vertexCount + 4 > bat.vertexBuffer.Length ||
            (bat.currentTextureView.HasValue && bat.currentTextureView.Value.Handle != texView.Handle))
        {
            Flush();
        }
        bat.currentTextureView = texView;

        var span = bat.vertexBuffer.InOut(bat.vertexCount, 4).Span;
        
        float x1 = position.X;
        float y1 = position.Y;
        float x2 = position.X + size.X;
        float y2 = position.Y + size.Y;

        // V0: Top-Left
        span[0] = new Vertex2D { position = new Vector2(x1, y1), uv = uvMin,                            color = color.Packed };
        // V1: Top-Right
        span[1] = new Vertex2D { position = new Vector2(x2, y1), uv = new Vector2(uvMax.X, uvMin.Y),    color = color.Packed };
        // V2: Bottom-Right
        span[2] = new Vertex2D { position = new Vector2(x2, y2), uv = uvMax,                            color = color.Packed };
        // V3: Bottom-Left
        span[3] = new Vertex2D { position = new Vector2(x1, y2), uv = new Vector2(uvMin.X, uvMax.Y),    color = color.Packed };

        bat.vertexCount += 4;
    }

    /// <summary>
    /// Draws a rotated quad transform around a normalized pivot (0..1).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawQuadRotated(in Vector2 position, in Vector2 size, float rotation, in Vector2 pivot, in Vector2 uvMin, in Vector2 uvMax, Color32 color, GpuTextureView? texture = null)
    {
        if (rotation == 0f) {
            DrawQuad(position - (pivot * size), size, uvMin, uvMax, color, texture);
            return;
        }

        var bat = batch;
        var texView = texture ?? bat.defaultWhiteTextureView;

        if (bat.vertexCount + 4 > bat.vertexBuffer.Length ||
            (bat.currentTextureView.HasValue && bat.currentTextureView.Value.Handle != texView.Handle))
        {
            Flush();
        }
        bat.currentTextureView = texView;

        var span = bat.vertexBuffer.InOut(bat.vertexCount, 4).Span;

        float cos = MathF.Cos(rotation);
        float sin = MathF.Sin(rotation);

        // Local offsets relative to pivot
        float l = -pivot.X * size.X;
        float r = (1f - pivot.X) * size.X;
        float t = -pivot.Y * size.Y;
        float b = (1f - pivot.Y) * size.Y;

        // V0: Top-Left
        span[0] = new Vertex2D { position = new Vector2(position.X + l * cos - t * sin, position.Y + l * sin + t * cos), uv = uvMin,                            color = color.Packed };
        // V1: Top-Right
        span[1] = new Vertex2D { position = new Vector2(position.X + r * cos - t * sin, position.Y + r * sin + t * cos), uv = new Vector2(uvMax.X, uvMin.Y),    color = color.Packed };
        // V2: Bottom-Right
        span[2] = new Vertex2D { position = new Vector2(position.X + r * cos - b * sin, position.Y + r * sin + b * cos), uv = uvMax,                            color = color.Packed };
        // V3: Bottom-Left
        span[3] = new Vertex2D { position = new Vector2(position.X + l * cos - b * sin, position.Y + l * sin + b * cos), uv = new Vector2(uvMin.X, uvMax.Y),    color = color.Packed };

        bat.vertexCount += 4;
    }
#endregion


#region Primitives / Shapes
    /// <summary>
    /// Helper to submit raw 4-point quad vertices using the default white texture.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawQuadRaw(in Vector2 v0, in Vector2 v1, in Vector2 v2, in Vector2 v3, Color32 color)
    {
        var bat = batch;
        var texView = bat.defaultWhiteTextureView;

        if (bat.vertexCount + 4 > bat.vertexBuffer.Length ||
            (bat.currentTextureView.HasValue && bat.currentTextureView.Value.Handle != texView.Handle))
        {
            Flush();
        }
        bat.currentTextureView = texView;

        var span = bat.vertexBuffer.InOut(bat.vertexCount, 4).Span;
        var uv = Vector2.Zero;

        span[0] = new Vertex2D { position = v0, uv = uv, color = color.Packed };
        span[1] = new Vertex2D { position = v1, uv = uv, color = color.Packed };
        span[2] = new Vertex2D { position = v2, uv = uv, color = color.Packed };
        span[3] = new Vertex2D { position = v3, uv = uv, color = color.Packed };

        bat.vertexCount += 4;
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

        DrawQuadRaw(
            start + normal, // V0: Top-Left
            end   + normal,   // V1: Top-Right
            end   - normal,   // V2: Bottom-Right
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
            DrawQuadRaw(center, p0, p1, p2, color);
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

            DrawQuadRaw(
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
    public void DrawString(ReadOnlySpan<char> text, Vector2 position, Color32 color, Font? font = null )
    {
        font ??= batch.GetDefaultFont();

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
                currentPos.Y += font.lineHeight;
                continue;
            }
            if (!font.TryGetGlyph(c, out var glyph)) {
                // Fallback for missing characters
                if (!font.TryGetGlyph('?', out glyph)) continue;
            }
            // Render glyph if it has visible dimensions (skips spaces)
            if (glyph.sourceSize.X > 0f && glyph.sourceSize.Y > 0f) {
                Vector2 renderPos = currentPos + glyph.offset;
                DrawSprite(renderPos, glyph.sourceSize, font.textureView, glyph.sourcePos, glyph.sourceSize, font.textureSize, color);
            }
            currentPos.X += glyph.advance;
        }
    }
    
    /// <summary>
    /// Calculates the bounding box size (width and height) of a text string in pixels.
    /// </summary>
    public Vector2 MeasureString(ReadOnlySpan<char> text, Font? font = null)
    {
        font ??= batch.GetDefaultFont();

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
            currentLineWidth += glyph.advance;
        }
        maxWidth = MathF.Max(maxWidth, currentLineWidth);
        
        return new Vector2(maxWidth, lineCount * font.lineHeight);
    }
    
    /// <summary>
    /// Draws text aligned relative to a bounding position or box.
    /// </summary>
    public void DrawStringAligned(ReadOnlySpan<char> text, Vector2 position, TextAlignment alignment, Color32 color, Font? font = null)
    {
        if (alignment == TextAlignment.Left)
        {
            DrawString(text, position, color, font);
            return;
        }

        Vector2 size = MeasureString(text, font);
        Vector2 alignedPos = position;

        if (alignment == TextAlignment.Center)
            alignedPos.X -= size.X * 0.5f;
        else if (alignment == TextAlignment.Right)
            alignedPos.X -= size.X;

        DrawString(text, alignedPos, color, font);
    }
    
    /// <summary>
    /// Truncates a string to fit within a maximum pixel width and appends '...'.
    /// Allocates a new string.
    /// </summary>
    public string TruncateWithEllipsis(ReadOnlySpan<char> text, float maxWidth, Font? font = null)
    {
        font ??= batch.GetDefaultFont();
        int visibleLength = GetVisibleLengthWithEllipsis(text, maxWidth, font);

        if (visibleLength >= text.Length)
            return text.ToString();

        return string.Concat(text[..visibleLength], "...");
    }

    /// <summary>
    /// Draws text at position, automatically truncating with '...' if it exceeds maxWidth.
    /// Allocation-free (GC-friendly).
    /// </summary>
    public void DrawStringTruncated(ReadOnlySpan<char> text, Vector2 position, float maxWidth, Color32 color, Font? font = null)
    {
        font ??= batch.GetDefaultFont();
        int visibleLength = GetVisibleLengthWithEllipsis(text, maxWidth, font);

        // If whole text fits, render normally
        if (visibleLength >= text.Length)
        {
            DrawString(text, position, color, font);
            return;
        }

        // Render visible substring directly without GC allocations
        DrawString(text[..visibleLength], position, color, font);

        // Calculate position for '...' and render it
        Vector2 ellipsisPos = position;
        for (int i = 0; i < visibleLength; i++)
        {
            if (font.TryGetGlyph(text[i], out var glyph))
                ellipsisPos.X += glyph.advance;
        }

        DrawString("...", ellipsisPos, color, font);
    }

    /// <summary>
    /// Internal core logic: Determines how many characters fit before '...' must be appended.
    /// </summary>
    private static int GetVisibleLengthWithEllipsis(ReadOnlySpan<char> text, float maxWidth, Font font)
    {
        if (!font.TryGetGlyph('.', out var dotGlyph))
            return text.Length;

        float ellipsisWidth = dotGlyph.advance * 3f;
        float currentWidth = ellipsisWidth;
        int visibleLength = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r' || c == '\n') break;
            if (!font.TryGetGlyph(c, out var glyph)) continue;

            if (currentWidth + glyph.advance > maxWidth) break;

            currentWidth += glyph.advance;
            visibleLength++;
        }

        return visibleLength;
    }

#endregion

    public void SetViewport(float width, float height)
    {
        if (batch.vertexStart != batch.vertexCount) {
            Flush();
        }
        var proj = Matrix4x4.CreateOrthographicOffCenter(0f, width, height, 0f, -1f, 1f);
        batch.uniforms = new ImUniforms { projection = proj };
    }

    public void Flush()
    {
        var bat = batch;
        int pendingVertices = bat.vertexCount - bat.vertexStart;
        if (pendingVertices <= 0) {
            return;
        }

        int pendingQuads = pendingVertices / 4;
        int indexCount   = pendingQuads * 6;

        var vertexView = bat.vertexBuffer.InOut(bat.vertexStart, pendingVertices).Write();
        var texture    = bat.currentTextureView ?? bat.defaultWhiteTextureView;
        var indexView  = bat.indexBuffer.In(0, indexCount);

        Batch2D.Draw(pass, bat.config, bat.uniforms, texture, bat.defaultSampler, vertexView, indexView);

        bat.vertexStart = bat.vertexCount;
    }
}