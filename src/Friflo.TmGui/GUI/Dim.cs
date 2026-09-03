// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Diagnostics;
using System.Numerics;

// ReSharper disable UnusedParameter.Global
// ReSharper disable RedundantSwitchExpressionArms
// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable InconsistentNaming
// ReSharper disable ArrangeThisQualifier
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;

/// <summary>
/// Specifies how a control dimensions itself relative to its content or parent.
/// </summary>
public enum Fit {
    /// <summary> Sizes the control automatically to fit its internal content. </summary>
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
    internal readonly   Sizing  sizingX;
    internal readonly   Sizing  sizingY;
    
    internal            float   Width       => X;
    internal            float   Height      => Y;

    internal            float   DistRight   => X;
    internal            float   DistBottom  => Y;
    
    internal            bool    IsAutoWidth     => sizingX != Sizing.Exact;
    internal            bool    IsAutoHeight    => sizingY != Sizing.Exact;
    
    // Returns true if both axes are explicitly bounded rather than auto-sized by content
    internal            bool    IsBounded       => sizingX != Sizing.Content && sizingY != Sizing.Content;
    

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
            Sizing.Fill     =>  $"width: Fill {DistRight} ➡️",
            _               =>  $"width: {Width}"
        };
        var y = sizingY switch {
            Sizing.Content  =>   "height: Content",
            Sizing.Fill     =>  $"height: Fill {DistBottom} ⬇️",
            _               =>  $"height: {Height}"
        };
        return $"{x}  {y}";
    }

#region Size
    /// <summary>Sizes both axes to explicit pixel values.</summary>
    [DebuggerHidden]
    public static Dim   Size(float width, float height) => new(width,   Sizing.Exact,   height, Sizing.Exact);
    
    /// <summary>Sizes both axes to explicit vector pixel values.</summary>
    [DebuggerHidden]
    public static Dim   Size(Vector2 size)              => new(size.X,  Sizing.Exact,   size.Y, Sizing.Exact);
    
    /// <summary>Sizes the width to an explicit value and height to inner content bounds.</summary>
    [DebuggerHidden]
    public static Dim   Size(float width, Fit Content)  => new(width,   Sizing.Exact,   0f,     Sizing.Content);

    /// <summary>Sizes the width to inner content bounds and height to an explicit value.</summary>
    [DebuggerHidden]
    public static Dim   Size(Fit Content, float height) => new(0f,      Sizing.Content, height, Sizing.Exact);
#endregion  


#region Fill
    /// <summary>Fills remaining parent width with a right margin and sets explicit height.</summary>
    [DebuggerHidden]
    public static Dim   Fill_X(float distRight, float height)   => new(distRight,   Sizing.Fill,    height,     Sizing.Exact);

    /// <summary>Fills remaining parent width with a right margin and sizes height to content.</summary>
    [DebuggerHidden]
    public static Dim   Fill_X(float distRight, Fit Content)    => new(distRight,   Sizing.Fill,    0f,         Sizing.Content);
    
    /// <summary>Sizes width to an explicit value and fills remaining parent height with a bottom margin.</summary>
    [DebuggerHidden]
    public static Dim   Fill_Y(float width, float distBottom)   => new(width,       Sizing.Exact,   distBottom, Sizing.Fill);

    /// <summary>Sizes width to inner content bounds and fills remaining parent height with a bottom margin.</summary>
    [DebuggerHidden]
    public static Dim   Fill_Y(Fit Content, float distBottom)   => new(0f,          Sizing.Content, distBottom, Sizing.Fill);
    
    /// <summary>Fills all available parent space on both axes.</summary>
    [DebuggerHidden]
    public static Dim   Fill()                                  => new(0f,          Sizing.Fill,    0f,         Sizing.Fill);
    
    /// <summary>Fills remaining parent space on both axes with a right and bottom margin.</summary>
    [DebuggerHidden]
    public static Dim   Fill(float distRight, float distBottom) => new(distRight,   Sizing.Fill,    distBottom, Sizing.Fill);
#endregion


#region Content
    /// <summary>Sizes both axes according to inner content bounds.</summary>
    [DebuggerHidden]
    public static Dim   Content() => default;
#endregion
}