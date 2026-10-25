// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;


// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ArrangeThisQualifier
// ReSharper disable UseIndexFromEndExpression
// ReSharper disable CheckNamespace
namespace Friflo.SmGui;

public enum SmColorKind : byte
{
    None    = 0,    // Zero-initialized default (default(SmColor))
    Value,          // Single Color32 value
    Span            // ReadOnlySpan<Color32>
}

public readonly ref struct TextColor
{
    public  readonly    ReadOnlySpan<Color32>   colors;     // 16 bytes
    public  readonly    Color32                 value;      //  4 bytes
    public  readonly    SmColorKind             kind;       //  1  byte

    public              bool                    IsNone      => kind == SmColorKind.None;
    public              bool                    IsSpan      => kind == SmColorKind.Span;

    public override     string                  ToString()  => kind.ToString();

    public TextColor(Color32 color)
    {
        value   = color;
        colors  = default;
        kind    = SmColorKind.Value;
    }

    public TextColor(ReadOnlySpan<Color32> colors)
    {
        this.colors = colors;
        this.value  = default;
        this.kind   = SmColorKind.Span;
    }

    public static implicit operator TextColor(Color32 color)                => new(color);
    public static implicit operator TextColor(ReadOnlySpan<Color32> colors) => new(colors);
    public static implicit operator TextColor(Color32[] colors)             => new(colors);

    public Color32 this[int index]
    {
        get
        {
            if (kind == SmColorKind.Span)
            {
                return (uint)index < (uint)colors.Length 
                    ? colors[index] 
                    : colors[colors.Length - 1];
            }
            // Applies to both Single and Unset (fallback value)
            return value; 
        }
    }
}

