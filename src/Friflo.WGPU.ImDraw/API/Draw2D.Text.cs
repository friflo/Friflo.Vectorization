// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using System.Text;

// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable MergeIntoPattern
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public readonly ref partial struct Draw2D
{
    /// <summary>
    /// Draws a text string using a bitmap font atlas.
    /// </summary>
    public Vector2 DrawText(ReadOnlySpan<char> text, Vector2 position, Color32 color, Font? font = null, float scale = 1.0f)
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
                DrawSpriteRegion(font.texture, renderPos, renderSize, glyph.sourcePos, glyph.sourceSize, font.textureSize, color);
            }
            currentPos.X += glyph.advance * scale;
        }
        return new Vector2(currentPos.X - position.X, font.lineHeight * scale);
    }
    
    /// <summary>
    /// Calculates the bounding box size (width and height) of a text string in pixels.
    /// </summary>
    public Vector2 MeasureText(ReadOnlySpan<char> text, Font? font = null, float scale = 1.0f)
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
    public void DrawTextAligned(ReadOnlySpan<char> text, Vector2 position, TextAlignment alignment, Color32 color, Font? font = null, float scale = 1.0f)
    {
        if (alignment == TextAlignment.Left)
        {
            DrawText(text, position, color, font, scale);
            return;
        }

        Vector2 size = MeasureText(text, font, scale);
        Vector2 alignedPos = position;

        if (alignment == TextAlignment.Center)
            alignedPos.X -= size.X * 0.5f;
        else if (alignment == TextAlignment.Right)
            alignedPos.X -= size.X;

        DrawText(text, alignedPos, color, font, scale);
    }
    
    /// <summary>
    /// Draws text aligned horizontally and vertically within a target bounding rectangle.
    /// Supports multi-line text and optional word wrapping.
    /// </summary>
    public void DrawTextInRect(
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
                float lineWidth = MeasureText(line, font, scale).X;

                if (horizontalAlignment == TextAlignment.Center)
                    lineX += (size.X - lineWidth) * 0.5f;
                else if (horizontalAlignment == TextAlignment.Right)
                    lineX += size.X - lineWidth;
            }

            DrawText(line, new Vector2(lineX, currentY), color, font, scale);
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
    public void DrawTextTruncated(ReadOnlySpan<char> text, Vector2 position, float maxWidth, Color32 color, Font? font = null, float scale = 1.0f)
    {
        font ??= batch.defaultFont;
        int visibleLength = GetVisibleLengthWithEllipsis(text, maxWidth, font, scale);

        // If whole text fits, render normally
        if (visibleLength >= text.Length)
        {
            DrawText(text, position, color, font, scale);
            return;
        }

        // Render visible substring directly without GC allocations
        DrawText(text[..visibleLength], position, color, font, scale);

        // Calculate position for '...' and render it
        Vector2 ellipsisPos = position;
        for (int i = 0; i < visibleLength; i++)
        {
            if (font.TryGetGlyph(text[i], out var glyph))
                ellipsisPos.X += glyph.advance * scale;
        }

        DrawText("...", ellipsisPos, color, font, scale);
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
    /// Wraps text by inserting line breaks ('\n'). Allocates a new string.
    /// </summary>
    public string WrapText(ReadOnlySpan<char> text, float maxWidth, Font? font = null, float scale = 1.0f)
    {
        if (text.IsEmpty || maxWidth <= 0f) return string.Empty;
        font ??= batch.defaultFont;

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
    public int DrawTextWrapped(ReadOnlySpan<char> text, Vector2 position, float maxWidth, Color32 color, Font? font = null, float scale = 1.0f)
    {
        if (text.IsEmpty || maxWidth <= 0f) return 0;
        font ??= batch.defaultFont;

        Vector2 currentPos = position;
        int lineCount = 0;

        foreach (ReadOnlySpan<char> line in GetWrappedLines(text, maxWidth, font, scale))
        {
            DrawText(line, currentPos, color, font, scale);
            currentPos.Y += font.lineHeight * scale;
            lineCount++;
        }
        return lineCount;
    }
    
    /// <summary>
    /// Helper method to create the line enumerator.
    /// </summary>
    private static WrappedLineEnumerator GetWrappedLines(ReadOnlySpan<char> text, float maxWidth, Font font, float scale)
    {
        return new WrappedLineEnumerator(text, maxWidth, font, scale);
    }
}

