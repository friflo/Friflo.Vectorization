// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Diagnostics;
using System.Numerics;

// ReSharper disable RedundantSwitchExpressionArms
// ReSharper disable ConvertToAutoPropertyWhenPossible
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
    private  readonly   float   X;
    private  readonly   float   Y;
    
    internal            float   Width       => X;
    internal            float   Height      => Y;

    internal            float   DistRight   => X;
    internal            float   DistBottom  => Y;
    
    internal readonly   Sizing  sizingX;
    internal readonly   Sizing  sizingY;
    
    internal            bool    IsAutoWidth     => sizingX != Sizing.Exact;
    internal            bool    IsAutoHeight    => sizingY != Sizing.Exact;
    
    // Returns true if both axes are explicitly bounded rather than auto-sized by content
    public              bool    IsBounded       => !IsAutoWidth && !IsAutoHeight;
    
    
    internal Vector2 ToSizeVector2(Vector2 available, Vector2 defaultSize)
    {
        var width = sizingX switch {
            Sizing.Exact   => Width,
            Sizing.Fill    => MathF.Max(0f, available.X - DistRight),
            Sizing.Content => defaultSize.X,
            _              => defaultSize.X
        };
        var height = sizingY switch {
            Sizing.Exact   => Height,
            Sizing.Fill    => MathF.Max(0f, available.Y - DistBottom),
            Sizing.Content => defaultSize.Y,
            _              => defaultSize.Y
        };
        return new Vector2(width, height);
    }

    internal Dim(float x, Sizing sizingX, float y, Sizing sizingY)
    {
        this.X          = x;
        this.sizingX    = sizingX;
        this.Y          = y;
        this.sizingY    = sizingY;
    }

    public override string ToString()
    {
        var x = sizingX switch {
            Sizing.Content  =>   "width: Content",
            Sizing.Fill     =>  $"width: fill {DistRight} from right",
            _               =>  $"width: {Width}"
        };
        var y = sizingY switch {
            Sizing.Content  =>   "height: Content",
            Sizing.Fill     =>  $"height: fill {DistBottom} from bottom",
            _               =>  $"height: {Height}"
        };
        return $"{x}, {y}";
    }

    #region Size
    /// <summary>Creates explicit fixed pixel bounds for both axes.</summary>
    [DebuggerHidden]
    public static Dim   Size(float width, float height)    => new(width,   Sizing.Exact,   height, Sizing.Exact);
    
    [DebuggerHidden]
    public static Dim   Size(Vector2 size)                  => new(size.X, Sizing.Exact,   size.Y, Sizing.Exact);
    
/// <summary>Sets exact width and explicitly specifies Y sizing mode.</summary>
    [DebuggerHidden]
    public static Dim   Size(float width, Fit _)          => new(width, Sizing.Exact, 0f, Sizing.Content);

    /// <summary>Sets exact height and explicitly specifies X sizing mode.</summary>
    [DebuggerHidden]
    public static Dim   Size(Fit _, float height)         => new(0f, Sizing.Content, height, Sizing.Exact);
#endregion  


#region Fill
    [DebuggerHidden]
    public static Dim   Fill_X(float distRight, float height)    => new(distRight,   Sizing.Fill,    height,     Sizing.Exact);

    [DebuggerHidden]
    public static Dim   Fill_X(float distRight, Fit _)           => new(distRight,   Sizing.Fill,    0f,         Sizing.Content);
    
    [DebuggerHidden]
    public static Dim   Fill_Y(float width, float distBottom)    => new(width,       Sizing.Exact,   distBottom, Sizing.Fill);

    [DebuggerHidden]
    public static Dim   Fill_Y(Fit _,       float distBottom)    => new(0f,          Sizing.Content, distBottom, Sizing.Fill);
    
    [DebuggerHidden]
    public static Dim   Fill()                                  => new(0f,          Sizing.Fill,    0f,         Sizing.Fill);
#endregion

    /// <summary>Sizes both axes according to inner content bounds.</summary>
    [DebuggerHidden]
    public static Dim   Content()               => new(0f,      Sizing.Content, 0f,     Sizing.Content);

    // --- Operator Conversions ---

    /// <summary>Allows passing explicit pixel sizes as a Vector2 tuple directly.</summary>
    [DebuggerHidden]
    public static implicit operator Dim((float width, float height) tuple) => Size(tuple.width, tuple.height);
    
    [DebuggerHidden]
    public static implicit operator Dim((float width, Fit fit) tuple) => Size(tuple.width, Fit.Content);
    
    [DebuggerHidden]
    public static implicit operator Dim((Fit fit, float height) tuple) => Size(Fit.Content, tuple.height);

    
    // [DebuggerHidden] public static implicit operator Dim(Vector2 size) => Size(size.X, size.Y);
}