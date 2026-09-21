// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable ArrangeThisQualifier
// ReSharper disable InconsistentNaming
namespace Friflo.TmGui.TUI;

public enum TuiColorMode
{
    Monochrome,
    RGB24
}

public struct TuiColorCell
{
    public  Rune        rune;           //  4 bytes
    public  TextStyle   textStyle;      //  1 byte
    public  byte        width;          //  1 byte      1 or 2: rune width in terminal. 0: ghost cells
    public  byte        sixelId;        //  1 byte
    public  Color32     color;          //  4 bytes
    public  Color32     background;     //  4 bytes
    
    public  char        Character   { get => throw new InvalidOperationException(); set => rune = new Rune(value); }
    
    public override string ToString() => $"'{rune}'";
}

public static class RuneExtensions
{
    extension (Rune rune)
    {
        /// <summary> Evaluates terminal column width (1 for standard/BMP, 2 for Wide/CJK/Plane-1 Emojis) </summary>
        public int RuneWidth {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                uint val = (uint)rune.Value;

                // Fast path for standard ASCII and Latin characters (< 0x2E80)
                if (val < 0x2E80) return 1;

                // Plane-1 Emojis and higher Unicode planes (> 0xFFFF)
                if (val > 0xFFFF) return 2;

                // CJK Radicals, Kanji & CJK Unified Ideographs (0x2E80 to 0x9FFF)
                if (val - 0x2E80 <= (0x9FFF - 0x2E80)) return 2;

                // Hangul Syllables (0xAC00 to 0xD7AF)
                return val - 0xAC00 <= (0xD7AF - 0xAC00) ? 2 : 1;
            }
        }
    }
}


[Flags]
public enum TextStyle : byte
{
    None            = 0,
    Bold            = 1 << 0, // \x1b[1m
    Dim             = 1 << 1, // \x1b[2m
    Italic          = 1 << 2, // \x1b[3m
    Underline       = 1 << 3, // \x1b[4m
    Inverse         = 1 << 4, // \x1b[7m
    StrikeThrough   = 1 << 5  // \x1b[9m
}

public struct TuiFocusBorder
{
    public  char    left;
    public  char    right;
    
    public override string ToString() => $"'{left}'  '{right}'";
    
    public TuiFocusBorder(char left, char right) {
        this.left   = left;
        this.right  = right;
    }
}

/// <summary> start and length of text within <see cref="TuiBatch.Texts"/> </summary>
[StructLayout(LayoutKind.Explicit, Size = 8)]
public struct TextSpan
{
    [FieldOffset(0)] public  char   fillChar;   //  2 bytes (+0)
    [FieldOffset(0)] public  int    start;      //  4 bytes
    [FieldOffset(4)] public  int    len;        //  4 bytes      case: len == 0   fillChar is used for fill rects
    
    public override string ToString() => $"[{start}..{start + len}]";
}

/// <summary>
/// If <see cref="len"/> == 0 - <see cref="value"/>
/// If <see cref="len"/>  > 0 - start and length of text within <see cref="TuiBatch.Colors"/>
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 6)]
public readonly struct Color32Span
{
    [FieldOffset(0)] public readonly    int     start;  //  4 bytes
    [FieldOffset(0)] public readonly    Color32 value;  //  4 bytes (+0)
    /// <summary> case: len == 0   color: .value    len == -1: no color </summary>
    [FieldOffset(4)] public readonly    short   len;    //  2 bytes
    
    public override                     string  ToString() => $"[{start}..{start + len}]";
    
    public Color32Span(int start, int len) {
        this.start  = start;
        this.len    = (short)len;
    }
    
    public Color32Span(Color32 value) {
        this.value  = value;
    }
    
    public Color32Span() {
        len = -1;
    }
}

/// <summary> A draw command within a <see cref="TuiBatch"/>.</summary>
/// <remarks>
/// Either a filled rectangle with passed background <see cref="color"/>.<br/>
/// Or a horizontal <see cref="text"/> with the passed <see cref="color"/>.
/// </remarks>
public struct TuiRect
{
    public  readonly    TextSpan    text;           //  8 bytes
    public              Vector2     TL;             //  8 bytes - top / lLeft    - Must use floats to enable layout mutations
    public              Vector2     BR;             //  8 bytes - bottom / right - Must use floats to enable layout mutations
    public  readonly    Color32Span color;          //  6 bytes
    public  readonly    TextStyle   textStyle;      //  1 byte
    public  readonly    byte        sixelId;        //  1 byte
    
    public  readonly    Vector2     Size        => BR - TL; // only for debugging
    
    public override string ToString()       => $"[{TL.X}, {TL.Y} | {Size.X}, {Size.Y}]";
    
    /// <summary> A filled rectangle with given background <see cref="color"/>. </summary>
    internal TuiRect(Vector2 pos, Vector2 size, Color32Span background, char fillChar) {
        text.fillChar   = fillChar;
        this.TL         = pos;
        this.BR         = pos + size;
        this.color      = background;
    }
    
    /// <summary> A sixel rectangle with given sixel id. </summary>
    internal TuiRect(byte sixelId, Vector2 pos, Vector2 size) {
        text.fillChar   = ' ';
        this.sixelId    = sixelId;
        this.TL         = pos;
        this.BR         = pos + size;
        this.color      = new Color32Span(Color32.Pink); // debugging color
    }
    
    /// <summary> A horizontal text with given text <see cref="color"/>. </summary>
    internal TuiRect(TextSpan text, TextStyle style, Vector2 pos, Vector2 size, Color32Span color) {
        this.text       = text;
        this.textStyle  = style;
        this.TL         = pos;
        this.BR         = pos + size;
        this.color      = color;
    }
}

internal readonly struct RectView
{
    public  readonly    int     offset;     //  4 bytes
    public  readonly    int     length;     //  4 bytes

    public override     string  ToString() => $"[{offset}..{offset + length}]";
    
    public RectView(int offset, int length) {
        this.offset = offset;
        this.length = length;
    }
}

internal readonly struct TuiRectCommand
{
    public  readonly    ulong       zIndex;       //  8 bytes
    public  readonly    int         sequence;     //  4 bytes
    public  readonly    RectView    rectView;     //  8 bytes
    public  readonly    Vector2     scissorTL;    //  8 bytes
    public  readonly    Vector2     scissorBR;    //  8 bytes

    public TuiRectCommand(
        ulong           zIndex,
        int             sequence,
        RectView        rectView,
        Vector2         scissorTl,
        Vector2         scissorBr)
    {
        this.zIndex     = zIndex;
        this.sequence   = sequence;
        this.rectView   = rectView;
        scissorTL       = scissorTl;
        scissorBR       = scissorBr;
    }

    public override string ToString() => $"zIndex: {zIndex} ({sequence})   views: {rectView.ToString()}";
}
