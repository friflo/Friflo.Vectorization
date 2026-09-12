// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public enum LayoutDirection
{
    Vertical,
    Horizontal
}

// Note: Is public to enable creation of custom widget methods like all build-in widgets. E.g. Spacer().
//       Basically the build-in widgets are Dogfooding the public Gui API.
public struct LayoutNode
{
    public readonly LayoutDirection direction;
    public readonly Vector2         startCursor;
    public          Vector2         cursor;
    public          Vector2         maxSize;    // Accrued content footprint (grows with widgets)
    public readonly Vector2         boundsSize; // Total boundary size assigned to this scope

    public override string ToString() => $"startCursor: {startCursor}  cursor: {cursor}  maxSize: {maxSize}  boundsSize: {boundsSize}";

    internal LayoutNode(LayoutDirection direction, Vector2 startCursor, Vector2 boundsSize) {
        this.direction      = direction;
        this.startCursor    = startCursor;
        cursor              = startCursor;
        this.boundsSize     = boundsSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly float WidgetFillWidth(float distRight, Vector2 minSize)
    {
        var remaining = boundsSize.X - (cursor.X - startCursor.X) - distRight;
        return remaining > minSize.X ? remaining : minSize.X;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly float WidgetFillHeight(float distBottom, Vector2 minSize)
    {
        var remaining = boundsSize.Y - (cursor.Y - startCursor.Y) - distBottom;
        return remaining > minSize.Y ? remaining : minSize.Y;
    }
}

internal enum ScrollAxis
{
    Vertical,   // 0 = Y-Axis,
    Horizontal  // 1 = X-Axis
}

internal readonly struct ScrollBar
{
    internal readonly   bool        visible;
    internal readonly   RectVector2 track;
    internal readonly   RectVector2 thumb;
    
    internal ScrollBar(Vector2 trackPos, Vector2 trackSize, Vector2 thumbPos, Vector2 thumbSize)
    {
        visible = true;
        track   = new RectVector2(trackPos, trackSize);
        thumb   = new RectVector2(thumbPos, thumbSize);
    }
}

internal readonly struct ScrollRange
{
    /// <summary> Maximum scrollable content offset (<c>contentSize - size</c>) in px. </summary>
    internal readonly   Vector2 maxScroll;
    
    /// <summary> Maximum draggable travel distance for the scrollbar thumb in px. </summary>
    internal readonly   Vector2 maxThumbTravel;
    
    /// <summary> Calculated size of the scrollbar thumb in px. </summary>
    internal readonly   Vector2 thumbSize;
    
    internal ScrollRange(Vector2 size, Vector2 contentSize, Vector2 minThumbSize)
    {
		var visibleRatio	= size / contentSize;
	    thumbSize           = Vector2.Max(minThumbSize, size * visibleRatio);
	    maxScroll           = Vector2.Max(default, contentSize - size);
	    maxThumbTravel	    = size - thumbSize;
    }
}

internal struct ScrollState
{
    public Vector2      offset;
    public bool         isHovered;          // 1 frame delay to capture dragging before subsequent widgets in scroll area
    public DragState    dragState;
    public Vector2      targetOffset;
    public bool         isDragging;
    public ScrollAxis   dragAxis;
    public Vector2      dragStartMouse;
    public Vector2      dragStartOffset;
    public Vector2      lastContentSize;    // Cached from previous frame
    public ScrollBar    horizontalBar;
    public ScrollBar    verticalBar;
}

internal struct ScrollAreaInfo
{
    public int      childId;
    public Vector2  pos;
    public Vector2  size;
}

[Flags]
internal enum ResizeEdge
{
    None        = 0,
    
    Top         = 1 << 0,
    Bottom      = 1 << 1,
    Left        = 1 << 2,
    Right       = 1 << 3,
    
    TopLeft     = Top    | Left,
    TopRight    = Top    | Right,
    BottomLeft  = Bottom | Left,
    BottomRight = Bottom | Right
}

internal struct FocusableEntry {
    internal    int     id;
    internal    Vector2 pos;
    internal    Vector2 size;
}

internal enum WindowState
{
    Visible,
    Created,
}

