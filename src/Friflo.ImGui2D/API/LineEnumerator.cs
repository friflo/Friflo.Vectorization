// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;


// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui2D;

/// <summary>
/// Allocation-free enumerator that yields word-wrapped line spans.
/// </summary>
internal ref struct WrappedLineEnumerator
{
    private readonly    ReadOnlySpan<char>  text;
    private readonly    float               maxWidth;
    private readonly    ImFont              font;
    private readonly    float               scale;

    private             int                 lineStart;
    private             int                 index;
    private             int                 lastSpace;
    private             float               currentLineWidth;
    private             float               widthAtLastSpace;
    private             bool                hasEnded;

    public              ReadOnlySpan<char>  Current { get; private set; }

    public WrappedLineEnumerator(ReadOnlySpan<char> text, float maxWidth, ImFont font, float scale = 1.0f)
    {
        this.text               = text;
        this.maxWidth           = maxWidth;
        this.font               = font;
        this.scale              = scale;
        this.lineStart          = 0;
        this.index              = 0;
        this.lastSpace          = -1;
        this.currentLineWidth   = 0f;
        this.widthAtLastSpace   = 0f;
        this.hasEnded           = text.IsEmpty || maxWidth <= 0f;
        this.Current            = default;
    }

    public WrappedLineEnumerator GetEnumerator() => this;

    public bool MoveNext()
    {
        if (hasEnded) return false;

        for (; index < text.Length; index++)
        {
            char c = text[index];

            if (c == '\r') continue;

            // Explicit newline
            if (c == '\n')
            {
                Current          = text[lineStart..index];
                index++;
                lineStart        = index;
                lastSpace        = -1;
                currentLineWidth = 0f;
                widthAtLastSpace = 0f;
                return true;
            }

            if (!font.TryGetGlyph(c, out var glyph))
            {
                if (!font.TryGetGlyph('?', out glyph)) continue;
            }

            float scaledAdvance = glyph.advance * scale;

            if (c == ' ')
            {
                lastSpace        = index;
                widthAtLastSpace = currentLineWidth + scaledAdvance;
            }

            // Line width exceeded?
            if (currentLineWidth + scaledAdvance > maxWidth && currentLineWidth > 0f)
            {
                if (lastSpace != -1 && lastSpace >= lineStart)
                {
                    // Break at last space
                    Current          = text[lineStart..lastSpace];
                    lineStart        = lastSpace + 1;
                    currentLineWidth = (currentLineWidth + scaledAdvance) - widthAtLastSpace;
                    lastSpace        = -1;
                }
                else
                {
                    // Hard wrap inside word
                    Current          = text[lineStart..index];
                    lineStart        = index;
                    currentLineWidth = scaledAdvance;
                }

                index++;
                return true;
            }

            currentLineWidth += scaledAdvance;
        }

        // Return trailing line
        Current  = text[lineStart..];
        hasEnded = true;
        return true;
    }
}