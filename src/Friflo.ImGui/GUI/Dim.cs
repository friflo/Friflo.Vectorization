// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Diagnostics;
using System.Numerics;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


/// <summary>  Defines how a layout dimension is calculated. </summary>
public enum Sizing : byte
{
    /** Size is determined by the inner content (children or text). */      Content,
    /** Explicit pixel size determined by the developer. */                 Exact,
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

#region Exact
    /// <summary>Creates explicit fixed pixel bounds for both axes.</summary>
    [DebuggerStepThrough]
    public static Dim   Exact(float width, float height)    => new(width,   Sizing.Exact,   height, Sizing.Exact);
    
    /// <summary>Creates explicit fixed pixel bounds for a single axis while keeping the other at Content.</summary>
    [DebuggerStepThrough]
    public static Dim   ExactX(float width)     => new(width,   Sizing.Exact,   0f,     Sizing.Content);

    [DebuggerStepThrough]
    public static Dim   ExactY(float height)    => new(0f,      Sizing.Content, height, Sizing.Exact);
#endregion  


#region Fill
    /// <summary>Fills remaining horizontal space. Height is optionally fixed or defaults to Content.</summary>
    [DebuggerStepThrough]
    public static Dim   FillX(float height)     => new(0f,      Sizing.Fill,    height, Sizing.Exact);

    [DebuggerStepThrough]
    public static Dim   FillX()                 => new(0f,      Sizing.Fill,    0f,     Sizing.Content);

    /// <summary>Fills remaining vertical space. Width is optionally fixed or defaults to Content.</summary>
    [DebuggerStepThrough]
    public static Dim   FillY(float width)      => new(width,   Sizing.Exact,   0f,     Sizing.Fill);

    [DebuggerStepThrough]
    public static Dim   FillY()                 => new(0f,      Sizing.Content, 0f,     Sizing.Fill);

    /// <summary>Fills remaining space in both directions.</summary>
    [DebuggerStepThrough]
    public static Dim   Fill()                  => new(0f,      Sizing.Fill,    0f,     Sizing.Fill);
    
    /// <summary>Fills remaining horizontal space while explicitly specifying Y sizing mode.</summary>
    [DebuggerStepThrough]
    public static Dim   FillX(Sizing sizingY)   => new(0f,      Sizing.Fill,    0f,     sizingY);

    /// <summary>Fills remaining vertical space while explicitly specifying X sizing mode.</summary>
    [DebuggerStepThrough]
    public static Dim   FillY(Sizing sizingX)   => new(0f,      sizingX,        0f,     Sizing.Fill);
#endregion

    /// <summary>Sizes both axes according to inner content bounds.</summary>
    [DebuggerStepThrough]
    public static Dim   Content()               => new(0f,      Sizing.Content, 0f,     Sizing.Content);

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