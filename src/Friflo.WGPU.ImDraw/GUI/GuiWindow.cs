// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

// ReSharper disable MergeIntoPattern
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable InvertIf
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


internal enum LayoutDirection
{
    Vertical,
    Horizontal
}

public struct LayoutNode
{
    internal LayoutDirection    direction;
    internal Vector2            startCursor;
    internal Vector2            maxSize;
}

public struct ScrollState
{
    public Vector2  offset;
    public Vector2  targetOffset;
    public bool     isDragging;
    public int      dragAxis;           // 0 = Y-Axis, 1 = X-Axis
    public Vector2  dragStartMouse;
    public Vector2  dragStartOffset;
}

[Flags]
public enum ResizeEdge
{
    None   = 0,
    Top    = 1 << 0,
    Bottom = 1 << 1,
    Left   = 1 << 2,
    Right  = 1 << 3,
    
    TopLeft     = Top    | Left,
    TopRight    = Top    | Right,
    BottomLeft  = Bottom | Left,
    BottomRight = Bottom | Right
}



public sealed class GuiWindow
{
    private  readonly   string              title;
    public              Vector2             Cursor { [DebuggerStepThrough] get => cursor; }
    
    private  readonly   GuiHost             host;
    internal            Vector2             pos;
    internal            Vector2             size;
    private  readonly   Vector2             minSize     = new(100f, 100f);
    private             ResizeEdge          activeResizeEdge;
    
    private             Vector2             cursor;
    private  readonly   Stack<int>          idStack         = new();
    private  readonly   Stack<LayoutNode>   layoutStack     = new();
    private             LayoutNode          currentLayout;
    public              LayoutNode          CurrentLayout   => currentLayout;
    private readonly    Dictionary<int, ScrollState> scrollStates = new(64);

    public   override   string              ToString() => title;


    internal GuiWindow(GuiHost host, string title) {
        this.host   = host;
        this.title  = title;
    }
    
    internal void ResetScope()
    {
        idStack.Clear();
        layoutStack.Clear();
        
        int baseHash = WidgetID.CombineHash(0, title.GetHashCode());
        idStack.Push(baseHash);
        currentLayout = new LayoutNode { direction = LayoutDirection.Vertical, startCursor = cursor, maxSize = Vector2.Zero };
        layoutStack.Push(currentLayout);
    }

    internal void ClearScope()
    {
        idStack.Clear();
        layoutStack.Clear();
        currentLayout = default;
    }
    
    internal void PushScope(WidgetID scopeId)
    {
        int currentHash = GetCurrentScopeHash();
        idStack.Push(scopeId.Resolve(currentHash));
    }

    internal void PopScope()
    {
        if (idStack.Count > 1) {
            idStack.Pop();
        }
    }

    public int GetCurrentScopeHash()
    {
        return idStack.Count > 0 ? idStack.Peek() : 0;
    }

    internal void PushLayout(LayoutDirection direction)
    {
        currentLayout = new LayoutNode { direction = direction, startCursor = cursor, maxSize = Vector2.Zero };
        layoutStack.Push(currentLayout);
    }

    internal Vector2 PopLayout()
    {
        if (layoutStack.Count > 1) {
            var finishedLayout = layoutStack.Pop();
            currentLayout = layoutStack.Peek();
            cursor = finishedLayout.startCursor;
            return finishedLayout.maxSize; // accumulated Bounding-Box of finished layout
        }
        currentLayout = default;
        return Vector2.Zero;
    }
    
    internal ref ScrollState GetOrCreateScrollState(int id)
    {
        ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(scrollStates, id, out bool exists);
        if (!exists) {
            // Initialize default state if first time seen
            state = new ScrollState {
                offset       = default,
                targetOffset = default
            };
        }
        return ref state;
    }
    
    internal void SetCursor(Vector2 value)
    {
        cursor = value;
    }

    public void MoveCursor(Vector2 widgetSize)
    {
        if (layoutStack.Count > 0) {
            var layout = layoutStack.Pop();
            
            if (layout.direction == LayoutDirection.Vertical) {
                cursor.Y += widgetSize.Y + 6f;
                layout.maxSize.X = Math.Max(layout.maxSize.X, widgetSize.X);
                layout.maxSize.Y += widgetSize.Y + 6f;
            } else {
                cursor.X += widgetSize.X + 6f;
                layout.maxSize.X += widgetSize.X + 6f;
                layout.maxSize.Y = Math.Max(layout.maxSize.Y, widgetSize.Y);
            }
            layoutStack.Push(layout);
        } else {
            cursor.Y += widgetSize.Y + 6f;
        }
    }
    
    public bool IsHoverAt(Vector2 widgetPos, Vector2 widgetSize, Draw2D draw)
    {
        var widgetRect = new RectVector2(widgetPos, widgetSize);
        
        // Is the mouse cursor inside the widget bounds?
        if (!widgetRect.Contains(host.input.MousePos)) {
            return false;
        }
        // Is the mouse cursor inside the currently active scissor clip region?
        var scissor = draw.batch.currentScissor;
        if (scissor.size.X > 0 && scissor.size.Y > 0) {
            if (!scissor.Contains(host.input.MousePos)) return false;
        }
        return host.IsTopWindowAt(host.input.MousePos, this);
    }

    public bool IsHoverAtCursor(Vector2 widgetSize, Draw2D draw)
    {
        return IsHoverAt(cursor, widgetSize, draw);
    }
    
#region resize

    internal bool ProcessResize(in GuiWidget drawGui, int resizeId, float border = 6f)
    {
        var input = drawGui.input;
        ResizeEdge hoverEdge = GetResizeEdge(input.MousePos, border);

        // Active state override: keep active while dragging even if mouse leaves border
        bool isHoverOrActive = hoverEdge != ResizeEdge.None || activeResizeEdge != ResizeEdge.None;
        var state = drawGui.GetWidgetState(isHoverOrActive, resizeId);

        // Determine which edge determines the cursor
        ResizeEdge effectiveEdge = activeResizeEdge != ResizeEdge.None ? activeResizeEdge : hoverEdge;

        if (effectiveEdge != ResizeEdge.None) {
            input.SetCursor(GetCursorForEdge(effectiveEdge));
        }

        if (state == WidgetState.Down) {
            if (activeResizeEdge == ResizeEdge.None) {
                activeResizeEdge = hoverEdge;
            }
            ApplyResize(input.MousePosDelta, activeResizeEdge);
            return true; // Strictly block titlebar dragging
        }

        activeResizeEdge = ResizeEdge.None;
        return false;
    }

    private static MouseCursor GetCursorForEdge(ResizeEdge edge)
    {
        return edge switch
        {
            ResizeEdge.Top      or ResizeEdge.Bottom        => MouseCursor.ResizeNS,
            ResizeEdge.Left     or ResizeEdge.Right         => MouseCursor.ResizeEW,
            ResizeEdge.TopLeft  or ResizeEdge.BottomRight   => MouseCursor.ResizeNWSE,
            ResizeEdge.TopRight or ResizeEdge.BottomLeft    => MouseCursor.ResizeNESW,
            _                                               => MouseCursor.Arrow
        };
    }

    private ResizeEdge GetResizeEdge(Vector2 mousePos, float border)
    {
        if (mousePos.X < pos.X - border || mousePos.X > pos.X + size.X + border ||
            mousePos.Y < pos.Y - border || mousePos.Y > pos.Y + size.Y + border)
        {
            return ResizeEdge.None;
        }

        ResizeEdge edge = ResizeEdge.None;

        if (mousePos.X <= pos.X + border)               edge |= ResizeEdge.Left;
        else if (mousePos.X >= pos.X + size.X - border) edge |= ResizeEdge.Right;

        if (mousePos.Y <= pos.Y + border)               edge |= ResizeEdge.Top;
        else if (mousePos.Y >= pos.Y + size.Y - border) edge |= ResizeEdge.Bottom;

        return edge;
    }

    private void ApplyResize(Vector2 delta, ResizeEdge edge)
    {
        // Horizontal: Right
        if ((edge & ResizeEdge.Right) != 0) {
            size.X = Math.Max(minSize.X, size.X + delta.X);
        }
        // Horizontal: Left
        if ((edge & ResizeEdge.Left) != 0) {
            float newWidth = Math.Max(minSize.X, size.X - delta.X);
            pos.X += size.X - newWidth;
            size.X = newWidth;
        }
        // Vertical: Bottom
        if ((edge & ResizeEdge.Bottom) != 0) {
            size.Y = Math.Max(minSize.Y, size.Y + delta.Y);
        }
        // Vertical: Top
        if ((edge & ResizeEdge.Top) != 0)
        {
            float newHeight = Math.Max(minSize.Y, size.Y - delta.Y);
            pos.Y += size.Y - newHeight;
            size.Y = newHeight;
        }
    }
#endregion

}