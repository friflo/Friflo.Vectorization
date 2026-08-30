// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Diagnostics;
using System.Numerics;
// ReSharper disable InconsistentNaming
// ReSharper disable ArrangeThisQualifier

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


public enum Fit {
    Content
}


/// <summary>  Defines how a layout dimension is calculated. </summary>
internal enum Sizing : byte
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
    internal readonly   float   X;
    internal readonly   float   Y;
    
    internal            float   Width       => X;
    internal            float   Height      => Y;

    internal            float   DistRight   => X;
    internal            float   DistBottom  => Y;
    
    internal readonly Sizing  sizingX;
    internal readonly Sizing  sizingY;

    internal Dim(float x, Sizing sizingX, float y, Sizing sizingY)
    {
        this.X          = x;
        this.sizingX    = sizingX;
        this.Y          = y;
        this.sizingY    = sizingY;
    }

#region Size
    /// <summary>Creates explicit fixed pixel bounds for both axes.</summary>
    [DebuggerStepThrough]
    public static Dim   Size(float width, float height)    => new(width,   Sizing.Exact,   height, Sizing.Exact);
    
/// <summary>Sets exact width and explicitly specifies Y sizing mode.</summary>
    [DebuggerStepThrough]
    public static Dim   Size(float width, Fit _)          => new(width, Sizing.Exact, 0f, Sizing.Content);

    /// <summary>Sets exact height and explicitly specifies X sizing mode.</summary>
    [DebuggerStepThrough]
    public static Dim   Size(Fit _, float height)         => new(0f, Sizing.Content, height, Sizing.Exact);
#endregion  


#region Fill
    [DebuggerStepThrough]
    public static Dim   FillX(float distRight, float height)    => new(distRight,   Sizing.Fill,    height,     Sizing.Exact);

    [DebuggerStepThrough]
    public static Dim   FillX(float distRight, Fit _)           => new(distRight,   Sizing.Fill,    0f,         Sizing.Content);
    
    [DebuggerStepThrough]
    public static Dim   FillY(float width, float distBottom)    => new(width,       Sizing.Exact,   distBottom, Sizing.Fill);

    [DebuggerStepThrough]
    public static Dim   FillY(Fit _,       float distBottom)    => new(0f,          Sizing.Content, distBottom, Sizing.Fill);
    
    [DebuggerStepThrough]
    public static Dim   Fill()                                  => new(0f,      Sizing.Fill,    0f,     Sizing.Fill);
#endregion

    /// <summary>Sizes both axes according to inner content bounds.</summary>
    [DebuggerStepThrough]
    public static Dim   Content()               => new(0f,      Sizing.Content, 0f,     Sizing.Content);

    // --- Operator Conversions ---

    /// <summary>Allows passing explicit pixel sizes as a Vector2 tuple directly.</summary>
    [DebuggerStepThrough]
    public static implicit operator Dim((float width, float height) tuple) => Size(tuple.width, tuple.height);
    
    [DebuggerStepThrough]
    public static implicit operator Dim((float width, Fit fit) tuple) => Size(tuple.width, Fit.Content);
    
    [DebuggerStepThrough]
    public static implicit operator Dim((Fit fit, float height) tuple) => Size(Fit.Content, tuple.height);



    /// <summary>Allows passing an explicit Vector2 directly for Fixed sizing.</summary>
    [DebuggerStepThrough]
    public static implicit operator Dim(Vector2 size) 
        => Size(size.X, size.Y);
}