// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;

public readonly struct MemoryView
{
    public  readonly    int     offset;     //  4 bytes
    public  readonly    int     length;     //  4 bytes

    public override     string  ToString() => $"[{offset}..{length}]";
    
    public MemoryView(int offset, int length) {
        this.offset = offset;
        this.length = length;
    }
}

public readonly struct DrawCommand(
    ulong           zIndex,
    int             sequence,
    in TmTexture    texture,
    MemoryView      vertexView,
    MemoryView      indexView,
    BlendState      blendState,
    in Matrix4x4    projection,
    SamplerFilter   samplerFilter,
    RectVector2     scissor)
{
    public readonly ulong           zIndex          = zIndex;           //  8 bytes
    public readonly int             sequence        = sequence;         //  4 bytes
    public readonly TmTexture       texture         = texture;          // 32 bytes
    public readonly MemoryView      vertexView      = vertexView;       //  8 bytes
    public readonly MemoryView      indexView       = indexView;        //  8 bytes
    public readonly BlendState      blendState      = blendState;       //  4 bytes
    public readonly Matrix4x4       projection      = projection;       // 64 bytes
    public readonly SamplerFilter   samplerFilter   = samplerFilter;    //  4 bytes
    public readonly RectVector2     scissor         = scissor;          // 16 bytes

    public override string ToString() => $"zIndex: {zIndex} ({sequence})   quads: {indexView.length / 4}   {texture}  {scissor}  {samplerFilter}";
}


internal struct CmdSegment
{
    internal    ulong   zIndex;
    internal    int     sequence;
    internal    int     index;
    internal    int     length;

    public override string ToString() => $"zIndex: {zIndex}, {sequence}   [{index}, {length}]";
}


public readonly struct RectVector2 (Vector2 pos, Vector2 size) : IEquatable<RectVector2> 
{
    public readonly     Vector2     pos  = pos;     // 8 bytes
    public readonly     Vector2     size = size;    // 8 bytes

    public override string ToString()       => $"[{pos.X}, {pos.Y} | {size.X}, {size.Y}]";

    public bool Equals(RectVector2 other)   => pos == other.pos && size == other.size;
    
    /// <summary> Checks if a point lies within the rectangle bounds. </summary>
    public bool Contains(Vector2 point)
    {
        return point.X >= pos.X && point.X <= pos.X + size.X &&
               point.Y >= pos.Y && point.Y <= pos.Y + size.Y;
    }

    /// <summary> Computes the intersection (overlapping region) of two rectangles. </summary>
    public RectVector2 Intersect(in RectVector2 other)
    {
        float x1 = MathF.Max(pos.X, other.pos.X);
        float y1 = MathF.Max(pos.Y, other.pos.Y);
        float x2 = MathF.Min(pos.X + size.X, other.pos.X + other.size.X);
        float y2 = MathF.Min(pos.Y + size.Y, other.pos.Y + other.size.Y);

        float w = MathF.Max(0f, x2 - x1);
        float h = MathF.Max(0f, y2 - y1);

        return new RectVector2(new Vector2(x1, y1), new Vector2(w, h));
    }
}




