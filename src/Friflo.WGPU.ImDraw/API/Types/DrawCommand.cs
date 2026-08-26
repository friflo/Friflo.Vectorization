// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using Shaders.Imdraw;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;

public readonly struct MemoryView
{
    public  readonly    int     offset;
    public  readonly    int     length;

    public override     string  ToString() => $"[{offset}..{length}]";
    
    public MemoryView(int offset, int length) {
        this.offset = offset;
        this.length = length;
    }
}

public struct DrawCommand
{
    public      int             zIndex;
    public      int             sequence;
    public      ImTexture       texture;
    public      MemoryView      vertexView;
    public      MemoryView      indexView;
    internal    RenderConfig    config;
    public      ImUniforms      uniforms;
    public      SamplerFilter   samplerFilter;
    public      RectVector2     scissor;

    public override string ToString() => $"zIndex: {zIndex} ({sequence})   quads: {indexView.length / 4}   {texture}  {scissor}  {samplerFilter}";
}


internal struct CmdSegment
{
    internal    int     zIndex;
    internal    int     sequence;
    internal    int     index;
    internal    int     length;

    public override string ToString() => $"zIndex: {zIndex}, {sequence}   [{index}, {length}]";
}


public readonly struct RectVector2 (Vector2 pos, Vector2 size) : IEquatable<RectVector2> 
{
    public readonly     Vector2     pos  = pos;
    public readonly     Vector2     size = size;

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
        float x1 = Math.Max(pos.X, other.pos.X);
        float y1 = Math.Max(pos.Y, other.pos.Y);
        float x2 = Math.Min(pos.X + size.X, other.pos.X + other.size.X);
        float y2 = Math.Min(pos.Y + size.Y, other.pos.Y + other.size.Y);

        float w = Math.Max(0f, x2 - x1);
        float h = Math.Max(0f, y2 - y1);

        return new RectVector2(new Vector2(x1, y1), new Vector2(w, h));
    }
}




