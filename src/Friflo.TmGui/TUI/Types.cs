// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable ArrangeThisQualifier
// ReSharper disable once CheckNamespace
// ReSharper disable InconsistentNaming

using System;

namespace Friflo.TmGui.TUI;

public struct TuiColorCell
{
    public  char        character;      //  2 bytes
    public  Color32     color;          //  4 bytes
    public  Color32     background;     //  4 bytes
    
    public override string ToString() => $"'{character}'";
}

internal struct TuiVector : IEquatable<TuiVector>
{
    internal        int x;      //  4 bytes
    internal        int y;      //  4 bytes

    public override string ToString() => $"({x}, {y})";

    internal TuiVector (float x, float y) {
        this.x = (int)x;
        this.y = (int)y;
    }
    
    public static   bool    operator ==(TuiVector left, TuiVector right) => left.x == right.x && left.y == right.y;
    public static   bool    operator !=(TuiVector left, TuiVector right) => !(left == right);
    
    public          bool    Equals(TuiVector other) => x == other.x && y == other.y;
    public override bool    Equals(object? obj) => obj is TuiVector other && Equals(other);
    public override int     GetHashCode() => HashCode.Combine(x, y);
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
    public          TuiVector   TL;             //  8 bytes - top / lLeft
    public          TuiVector   BR;             //  8 bytes - bottom / right
    public          Color32     color;          //  4 bytes
    public          Color32     background;     //  4 bytes
    
    public override string      ToString()       => $"[{TL.x}, {TL.y} | {BR.x}, {BR.y}]";
    
    internal TuiRect(TuiVector tl, TuiVector size, Color32 background) {
        this.TL             = tl;
        this.BR             = new TuiVector(tl.x + size.x, tl.y + size.y);
        this.background     = background;
    }
        
    internal TuiRect(TextSpan text, TuiVector tl, TuiVector br, Color32 color, Color32 background) {
        this.text           = text;
        this.TL             = tl;
        this.BR             = br;
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
    public  readonly    TuiVector   scissorTL;    //  8 bytes
    public  readonly    TuiVector   scissorBR;    //  8 bytes

    public TuiRectCommand(
        ulong           zIndex,
        int             sequence,
        RectView        rectView,
        TuiVector       scissorTl,
        TuiVector       scissorBr)
    {
        this.zIndex     = zIndex;
        this.sequence   = sequence;
        this.rectView   = rectView;
        scissorTL       = scissorTl;
        scissorBR       = scissorBr;
    }

    public override string ToString() => $"zIndex: {zIndex} ({sequence})   views: {rectView.ToString()}";
}
