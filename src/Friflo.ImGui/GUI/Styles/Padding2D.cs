// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui2D;

[StructLayout(LayoutKind.Sequential)]
public readonly struct Padding2D : IEquatable<Padding2D>
{
    // Min = (Left, Top), Max = (Right, Bottom)
    public readonly     Vector2     Min;
    public readonly     Vector2     Max;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Padding2D(float uniform)
    {
        Min = new Vector2(uniform);
        Max = new Vector2(uniform);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Padding2D(float horizontal, float vertical)
    {
        Min = new Vector2(horizontal, vertical);
        Max = new Vector2(horizontal, vertical);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Padding2D(Vector2 min, Vector2 max)
    {
        Min = min;
        Max = max;
    }
    
    // Grouped by axis: X-Axis (Left, Right), Y-Axis (Top, Bottom)
    public Padding2D(float left, float right, float top, float bottom)
    {
        Min = new Vector2(left, top);
        Max = new Vector2(right, bottom);
    }

    public  float   Left    => Min.X;
    public  float   Top     => Min.Y;
    public  float   Right   => Max.X;
    public  float   Bottom  => Max.Y;

    public  Vector2 Size            => Min + Max;
    public  float   Vertical        => Min.Y + Max.Y;

    public static Padding2D Zero => default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 Expand(Vector2 size) => size + Size;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 Shrink(Vector2 size) => size - Size;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Padding2D(float uniform) => new(uniform);

    public          bool    Equals(Padding2D other) => Min == other.Min && Max == other.Max;
    public override bool    Equals(object? obj)     => obj is Padding2D other && Equals(other);
    public override int     GetHashCode()           => HashCode.Combine(Min, Max);
}