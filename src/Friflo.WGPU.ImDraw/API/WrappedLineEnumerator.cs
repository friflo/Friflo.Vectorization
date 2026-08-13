// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;


// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;

/// <summary>
/// Allocation-free enumerator that yields word-wrapped line spans.
/// </summary>
public ref struct WrappedLineEnumerator
{
    private readonly    ReadOnlySpan<char> text;
    private readonly    float   maxWidth;
    private readonly    Font    font;

    private             int     lineStart;
    private             int     index;
    private             int     lastSpace;
    private             float   currentLineWidth;
    private             float   widthAtLastSpace;
    private             bool    hasEnded;

    public ReadOnlySpan<char> Current { get; private set; }

    public WrappedLineEnumerator(ReadOnlySpan<char> text, float maxWidth, Font font)
    {
        this.text = text;
        this.maxWidth = maxWidth;
        this.font = font;
        lineStart = 0;
        index = 0;
        lastSpace = -1;
        currentLineWidth = 0f;
        widthAtLastSpace = 0f;
        hasEnded = text.IsEmpty || maxWidth <= 0f;
        Current = default;
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
                Current = text[lineStart..index];
                index++;
                lineStart = index;
                lastSpace = -1;
                currentLineWidth = 0f;
                widthAtLastSpace = 0f;
                return true;
            }

            if (!font.TryGetGlyph(c, out var glyph))
            {
                if (!font.TryGetGlyph('?', out glyph)) continue;
            }

            if (c == ' ')
            {
                lastSpace = index;
                widthAtLastSpace = currentLineWidth + glyph.advance;
            }

            // Line width exceeded?
            if (currentLineWidth + glyph.advance > maxWidth && currentLineWidth > 0f)
            {
                if (lastSpace != -1 && lastSpace >= lineStart)
                {
                    // Break at last space
                    Current = text[lineStart..lastSpace];
                    lineStart = lastSpace + 1;
                    currentLineWidth = (currentLineWidth + glyph.advance) - widthAtLastSpace;
                    lastSpace = -1;
                }
                else
                {
                    // Hard wrap inside word
                    Current = text[lineStart..index];
                    lineStart = index;
                    currentLineWidth = glyph.advance;
                }

                index++;
                return true;
            }

            currentLineWidth += glyph.advance;
        }

        // Return trailing line
        Current = text[lineStart..];
        hasEnded = true;
        return true;
    }
}