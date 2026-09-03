// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;


// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;

/// <summary>
/// Allocation-free enumerator that yields word-wrapped line spans.
/// </summary>
internal ref struct WrappedLineEnumerator
{
    private readonly    ReadOnlySpan<char>  text;
    private readonly    float               maxWidth;
    private readonly    TmFont              font; 
    private readonly    float               scale;
    private readonly    float               lineHeight;

    private             int                 lineStart;
    private             int                 index;
    private             int                 lastSpace;
    private             float               currentLineWidth;
    private             float               widthAtLastSpace;
    private             bool                hasEnded;

    public              ReadOnlySpan<char>  Current { get; private set; }

    public WrappedLineEnumerator(ReadOnlySpan<char> text, float maxWidth, TmFont font, bool isTui, float scale)
    {
        this.text               = text;
        this.maxWidth           = maxWidth;
        this.font               = font;
        this.scale              = scale;
        this.lineHeight         = isTui ? font.lineHeight : 0;
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
        
        float scaledAdvance = lineHeight;

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

            if (lineHeight == 0) {
                if (!font.TryGetGlyph(c, out var glyph))
                {
                    if (!font.TryGetGlyph('?', out glyph)) continue;
                }
                scaledAdvance = glyph.advance * scale;
            }
            if (c == ' ')
            {
                lastSpace        = index;
                widthAtLastSpace = currentLineWidth + scaledAdvance;
            }

            // Line width exceeded?
            if (currentLineWidth + scaledAdvance > maxWidth && currentLineWidth > 0f)
            {
                if (lastSpace != -1 && lastSpace >= lineStart) {
                    // Break at last space
                    Current          = text[lineStart..lastSpace];
                    lineStart        = lastSpace + 1;
                    currentLineWidth = (currentLineWidth + scaledAdvance) - widthAtLastSpace;
                    lastSpace        = -1;
                } else  {
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