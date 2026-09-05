// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Numerics;


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
    public  char        character;      //  2 bytes
    public  Color32     color;          //  4 bytes
    public  Color32     background;     //  4 bytes
    
    public override string ToString() => $"'{character}'";
}

public struct TuiBorder
{
    public  char    left;
    public  char    right;
    
    public override string ToString() => $"'{left}'  '{right}'";
    
    public TuiBorder(char left, char right) {
        this.left   = left;
        this.right  = right;
    }
}

internal struct TextSpan
{
    internal int    start;  //  4 bytes
    internal int    len;    //  4 bytes
    
    public override string ToString() => $"[{start}..{start + len}]";
}

internal struct TuiRect
{
    public          TextSpan    text;           //  8 bytes
    public          Vector2     TL;             //  8 bytes - top / lLeft
    public          Vector2     BR;             //  8 bytes - bottom / right
    public          Color32     color;          //  4 bytes
    public          Color32     background;     //  4 bytes
    
    public override string      ToString()       => $"[{TL.X}, {TL.Y} | {BR.X}, {BR.Y}]";
    
    internal TuiRect(Vector2 pos, Vector2 size, Color32 background) {
        this.TL             = pos;
        this.BR             = pos + size;
        this.background     = background;
    }
        
    internal TuiRect(TextSpan text, Vector2 pos, Vector2 size, Color32 color, Color32 background) {
        this.text           = text;
        this.TL             = pos;
        this.BR             = pos + size;
        this.color          = color;
        this.background     = background;
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
