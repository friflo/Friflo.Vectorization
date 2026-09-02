// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui2D.TUI;

public struct TuiCell
{
    internal char       character;      //  2 bytes
    internal Color32    color;          //  4 bytes
    internal Color32    background;     //  4 bytes
    
    public override string ToString() => $"'{character}'";
}

public struct TuiVector
{
    internal    int x;      //  4 bytes
    internal    int y;      //  4 bytes

    public override string ToString() => $"({x}, {y})";

    internal TuiVector (float x, float y) {
        this.x = (int)x;
        this.y = (int)y;
    }
}

public struct TextSpan
{
    internal int    start;  //  4 bytes
    internal int    len;    //  4 bytes
    
    public override string ToString() => $"[{start}..{start + len}]";
}

public readonly struct TuiRect
{
    public readonly     TextSpan    text;       //  8 bytes
    public readonly     TuiVector   pos;        //  8 bytes
    public readonly     TuiVector   size;       //  8 bytes
    public readonly     Color32     color;      //  4 bytes
    public readonly     Color32     background; //  4 bytes
    
    public override     string      ToString()       => $"[{pos.x}, {pos.y} | {size.x}, {size.y}]";
    
    internal TuiRect(TuiVector pos, TuiVector size, Color32 background) {
        this.pos        = pos;
        this.size       = size;
        this.background = background;
    }
        
    internal TuiRect(TextSpan text, TuiVector pos, Color32 color, Color32 background) {
        this.text       = text;
        this.pos        = pos;
        this.color      = color;
        this.background = background;
    }
}

public readonly struct RectView
{
    public  readonly    int     offset;     //  4 bytes
    public  readonly    int     length;     //  4 bytes

    public override     string  ToString() => $"[{offset}..{length}]";
    
    public RectView(int offset, int length) {
        this.offset = offset;
        this.length = length;
    }
}

public readonly struct TuiRectCommand(
    ulong           zIndex,
    int             sequence,
    RectView        rectView,
    RectVector2     scissor)
{
    public readonly ulong           zIndex      = zIndex;       //  8 bytes
    public readonly int             sequence    = sequence;     //  4 bytes
    public readonly RectView        rectView    = rectView;     //  8 bytes
    public readonly RectVector2     scissor     = scissor;      // 16 bytes

    public override string ToString() => $"zIndex: {zIndex} ({sequence})   quads: {rectView.length}";
}
