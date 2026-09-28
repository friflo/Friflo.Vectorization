// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace Friflo.TmGui.TUI;


internal struct DrawSixel
{
    internal readonly   byte        sixelId;
    internal readonly   TuiSixel    sixel;
    /// <summary> The hash changes if sixel pixels are modified or its position or size is changed. </summary>
    internal            uint        sixelHash;
    private             uint        drawCellsHash;
    internal            bool        draw;
    internal            int         left;
    internal            int         top;
    internal            int         right;
    internal            int         bottom;
    internal            Vector2     pos;  // screen space. Not terminal pixel position
    internal            Vector2     size; // screen space. Not terminal pixel position

    public   override   string      ToString() => sixel == null ? "null" : $"x: {pos.X} y: {pos.Y}  {sixel}";

    private  const uint FnvOffsetBasis32 = 0x811C9DC5;
    private  const uint FnvPrime32       = 0x01000193;
    
    internal uint UpdateHash()
    {
        return sixelHash = (uint)(sixel.GetHashCode() ^ sixel.Version ^ pos.GetHashCode() ^ size.GetHashCode()) ^ drawCellsHash;
    }
    
    internal DrawSixel(byte sixelId, TuiSixel sixel, Vector2 pos, Vector2 size)
    {
        this.sixelId    = sixelId;
        this.sixel      = sixel;
        this.pos        = pos;
        this.size       = size;
        
        drawCellsHash   = FnvOffsetBasis32;
        left            = 10_000;
        top             = 10_000;
        right           = -1;
        bottom          = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Draw(int x, int y, uint cellHash)
    {
        draw            = true;
        // left         = Math.Min(left,  x);
        // right        = Math.Max(right, x);
        // top          = Math.Min(top,   y);
        if (x < left)  left     = x;
        if (x > right) right    = x;
        if (y < top)   top      = y;
        bottom                  = y;
        drawCellsHash   = (drawCellsHash ^ cellHash) * FnvPrime32;
    }
}

