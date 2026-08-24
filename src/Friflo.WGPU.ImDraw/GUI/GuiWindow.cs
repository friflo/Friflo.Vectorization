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

public enum ScrollAxis
{
    Vertical,   // 0 = Y-Axis,
    Horizontal  // 1 = X-Axis
}

public struct ScrollState
{
    public Vector2      offset;
    public Vector2      targetOffset;
    public bool         isDragging;
    public ScrollAxis   dragAxis;
    public Vector2      dragStartMouse;
    public Vector2      dragStartOffset;
}

internal struct ScrollAreaInfo
{
    public int      childId;
    public Vector2  pos;
    public Vector2  size;
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
    private             Vector2             activeResizeSize;
    private             Vector2             activeResizeMousePos;
    
    private             Vector2             cursor;
    private  readonly   Stack<int>          idStack         = new();
    private  readonly   List<LayoutNode>    layoutStack     = [];
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
        layoutStack.Add(currentLayout);
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
        layoutStack.Add(currentLayout);
    }

    internal Vector2 PopLayout()
    {
        int lastIdx = layoutStack.Count - 1;
        if (lastIdx < 0) {
            return Vector2.Zero;
        }
        var finishedLayout = layoutStack[lastIdx];
        layoutStack.RemoveAt(lastIdx);

        if (lastIdx > 0) {
            cursor = finishedLayout.startCursor;
            MoveCursor(finishedLayout.maxSize);
        }
        return finishedLayout.maxSize;
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
        const float spacing = 6f;

        if (layoutStack.Count == 0) {
            cursor.Y += widgetSize.Y + spacing;
            return;
        }
        ref var layout = ref CollectionsMarshal.AsSpan(layoutStack)[layoutStack.Count - 1];

        if (layout.direction == LayoutDirection.Vertical) {
            cursor.Y += widgetSize.Y + spacing;
            layout.maxSize.X = Math.Max(layout.maxSize.X, widgetSize.X);
            layout.maxSize.Y += widgetSize.Y + spacing;
        } else {
            cursor.X += widgetSize.X + spacing;
            layout.maxSize.X += widgetSize.X + spacing;
            layout.maxSize.Y = Math.Max(layout.maxSize.Y, widgetSize.Y);
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

    internal bool ProcessResize(in GuiWidget drawGui, int resizeId, float border = 15f)
    {
        var input = drawGui.input;
        var hoverEdge       = GetResizeEdge(input.MousePos, border);
        var isHoverOrActive = hoverEdge != ResizeEdge.None || activeResizeEdge != ResizeEdge.None;
        var state           = drawGui.GetDragState(isHoverOrActive, resizeId);
        
        if (state == WidgetState.Down) {
            if (activeResizeEdge == ResizeEdge.None) {
                activeResizeEdge        = GetResizeEdge(input.MousePos, border);
                activeResizeSize        = size;
                activeResizeMousePos    = input.MousePos;
                drawGui.draw.batch.host.SetTopWindow(this);
            } else {
                var offset = input.MousePos - activeResizeMousePos;
                ApplyResize(offset, activeResizeEdge);
            }
            input.SetCursor(GetCursorForEdge(activeResizeEdge));
            return true;
        }
        activeResizeEdge = ResizeEdge.None;
        if (hoverEdge != ResizeEdge.None && !input.IsDragActive) {
            input.SetCursor(GetCursorForEdge(hoverEdge));
        }
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
        
        if (edge != ResizeEdge.None) {
            var topMost = host.GetTopWindowAt(host.input.MousePos);
            if (topMost == null || topMost == this) {
                return edge;
            }
        }
        return ResizeEdge.None;
    }

    private void ApplyResize(Vector2 offset, ResizeEdge edge)
    {
        // Horizontal: Right
        if ((edge & ResizeEdge.Right) != 0) {
            size.X = Math.Max(minSize.X, activeResizeSize.X + offset.X);
        }
        // Horizontal: Left
        if ((edge & ResizeEdge.Left) != 0) {
            float newWidth = Math.Max(minSize.X, activeResizeSize.X - offset.X);
            pos.X += size.X - newWidth;
            size.X = newWidth;
        }
        // Vertical: Bottom
        if ((edge & ResizeEdge.Bottom) != 0) {
            size.Y = Math.Max(minSize.Y, activeResizeSize.Y + offset.Y);
        }
        // Vertical: Top
        if ((edge & ResizeEdge.Top) != 0)
        {
            float newHeight = Math.Max(minSize.Y, activeResizeSize.Y - offset.Y);
            pos.Y += size.Y - newHeight;
            size.Y = newHeight;
        }
    }
#endregion

#region scroll area

    private readonly    Stack<ScrollAreaInfo>   scrollAreaStack     = new();
    private             ScrollAreaInfo          CurrentScrollArea   => scrollAreaStack.Count > 0 ? scrollAreaStack.Peek() : default;

    internal void PushScrollAreaInfo(int childId, Vector2 areaPos, Vector2 areaSize) {
        scrollAreaStack.Push(new ScrollAreaInfo { childId = childId, pos = areaPos, size = areaSize });
    }

    internal void PopScrollAreaInfo()
    {
        if (scrollAreaStack.Count > 0) {
            scrollAreaStack.Pop();
        }
    }
    
    public void EnsureVisibleInScrollArea(Vector2 widgetPos, Vector2 widgetSize)
    {
        if (!host.input.JustNavigated) {
            return;
        }
        var scrollArea = CurrentScrollArea;
        if (scrollArea.childId == 0) return; // Not inside an active ScrollArea

        ref var scrollState = ref GetOrCreateScrollState(scrollArea.childId);
        float padding = 8f;

        // Check and adjust vertical scrolling (Y-Axis)
        float widgetTop = widgetPos.Y;
        float widgetBottom = widgetPos.Y + widgetSize.Y;
        float areaTop = scrollArea.pos.Y;
        float areaBottom = scrollArea.pos.Y + scrollArea.size.Y;

        if (widgetTop < areaTop + padding) {
            float delta = (areaTop + padding) - widgetTop;
            scrollState.offset.Y = Math.Max(0f, scrollState.offset.Y - delta);
        } else if (widgetBottom > areaBottom - padding) {
            float delta = widgetBottom - (areaBottom - padding);
            scrollState.offset.Y += delta;
        }

        // Check and adjust horizontal scrolling (X-Axis)
        float widgetLeft = widgetPos.X;
        float widgetRight = widgetPos.X + widgetSize.X;
        float areaLeft = scrollArea.pos.X;
        float areaRight = scrollArea.pos.X + scrollArea.size.X;

        if (widgetLeft < areaLeft + padding) {
            float delta = (areaLeft + padding) - widgetLeft;
            scrollState.offset.X = Math.Max(0f, scrollState.offset.X - delta);
        } else if (widgetRight > areaRight - padding) {
            float delta = widgetRight - (areaRight - padding);
            scrollState.offset.X += delta;
        }
    }
#endregion
}