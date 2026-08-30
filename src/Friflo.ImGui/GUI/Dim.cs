// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Diagnostics;
using System.Numerics;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


/// <summary>  Defines how a layout dimension is calculated. </summary>
public enum Sizing : byte
{
    /** Explicit pixel size determined by the developer. */                 Exact,
    /** Size is determined by the inner content (children or text). */      Content,
    /** Takes up the remaining available space in the parent container. */  Fill
}

/// <summary>
/// Represents a two-dimensional layout specification for widgets and containers.
/// </summary>
public readonly struct Dim
{
    public readonly float   width;
    public readonly float   height;
    public readonly Sizing  sizingX;
    public readonly Sizing  sizingY;

    public Dim(float width, Sizing sizingX, float height, Sizing sizingY)
    {
        this.width      = width;
        this.sizingX    = sizingX;
        this.height     = height;
        this.sizingY    = sizingY;
    }

    // --- Factory Methods ---

    /// <summary>Creates explicit fixed pixel bounds for both axes.</summary>
    [DebuggerStepThrough]
    public static Dim Exact(float width, float height)  => new(width,   Sizing.Exact,   height, Sizing.Exact);

    /// <summary>Fills remaining horizontal space. Height is optionally fixed or defaults to Content.</summary>
    [DebuggerStepThrough]
    public static Dim FillX(float height)               => new(0f,      Sizing.Fill,    height, Sizing.Exact);

    [DebuggerStepThrough]
    public static Dim FillX()                           => new(0f,      Sizing.Fill,    0f,     Sizing.Content);

    /// <summary>Fills remaining vertical space. Width is optionally fixed or defaults to Content.</summary>
    [DebuggerStepThrough]
    public static Dim FillY(float width)                => new(width,   Sizing.Exact,   0f,     Sizing.Fill);

    [DebuggerStepThrough]
    public static Dim FillY()                           => new(0f,      Sizing.Content, 0f,     Sizing.Fill);

    /// <summary>Fills remaining space in both directions.</summary>
    [DebuggerStepThrough]
    public static Dim Fill()                            => new(0f,      Sizing.Fill,    0f,     Sizing.Fill);

    /// <summary>Sizes both axes according to inner content bounds.</summary>
    [DebuggerStepThrough]
    public static Dim Content()                         => new(0f,      Sizing.Content, 0f,     Sizing.Content);

    // --- Operator Conversions ---

    /// <summary>Allows passing explicit pixel sizes as a Vector2 tuple directly.</summary>
    [DebuggerStepThrough]
    public static implicit operator Dim((float width, float height) tuple) 
        => Exact(tuple.width, tuple.height);

    /// <summary>Allows passing an explicit Vector2 directly for Fixed sizing.</summary>
    [DebuggerStepThrough]
    public static implicit operator Dim(Vector2 size) 
        => Exact(size.X, size.Y);
}