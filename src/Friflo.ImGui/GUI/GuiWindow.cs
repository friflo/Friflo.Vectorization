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
namespace Friflo.ImGui;


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
    public          Vector2         maxSize;
    
    internal LayoutNode(LayoutDirection direction, Vector2 startCursor) {
        this.direction      = direction;
        this.startCursor    = startCursor;
        cursor              = startCursor;
    }
}

internal enum ScrollAxis
{
    Vertical,   // 0 = Y-Axis,
    Horizontal  // 1 = X-Axis
}

internal struct ScrollState
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
internal enum ResizeEdge
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

internal struct FocusableEntry {
    internal    int     id;
    internal    Vector2 pos;
    internal    Vector2 size;
}


public sealed class GuiWindow
{
    private  readonly       string          title;
    private  readonly       GuiHost         host;
    
    internal                RectVector2     bounds;
    internal                Vector2         Pos                 { [DebuggerHidden] get => bounds.pos; }
    internal                Vector2         Size                { [DebuggerHidden] get => bounds.size; }

    private  readonly       Vector2         minSize             = new(100f, 100f);
    private                 ResizeEdge      activeResizeEdge;
    private                 Vector2         activeResizeSize;
    
    private  readonly       Stack<int>      idStack             = new();
    private                 LayoutNode[]    layoutStack         = [default];
    private                 int             layoutStackCount;
    public   ref readonly   LayoutNode      CurrentLayout       => ref layoutStack[layoutStackCount - 1];
    internal        ref     LayoutNode      CurrentLayoutRef    => ref layoutStack[layoutStackCount - 1];
    public                  Vector2         Cursor              =>     layoutStack[layoutStackCount - 1].cursor;
    
    private  readonly       Dictionary<int, ScrollState>    scrollStates        = new(64);
    
    // --- tab / 2D array key navigation
    internal readonly       List<FocusableEntry>            currentFocusables   = new(32);
    internal readonly       List<FocusableEntry>            prevFocusables      = new(32);

    public   override       string          ToString() => title;


    internal GuiWindow(GuiHost host, string title) {
        this.host   = host;
        this.title  = title;
    }
    
    internal void NewFrame()
    {
        // --- tab / 2D array key navigation ---
        // Swap buffer for spatial queries
        prevFocusables.Clear();
        prevFocusables.AddRange(currentFocusables);
        currentFocusables.Clear();
    }
    
    internal void ResetScope()
    {
        idStack.Clear();
        layoutStackCount = 1;
        
        int baseHash = WidgetID.CombineHash(0, title.GetHashCode());
        idStack.Push(baseHash);
        layoutStack[0] = new LayoutNode(LayoutDirection.Vertical, Pos);
    }

    internal void ClearScope()
    {
        idStack.Clear();
        layoutStackCount = 0;
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

    /// <summary>
    /// Note: Internal state-only push. Must only be invoked via <see cref="GuiWidget.PushLayout"/>
    /// to ensure symmetry with <see cref="GuiWidget.PopLayout"/>.
    /// </summary>
    internal void PushLayout(LayoutDirection direction)
    {
        var count = layoutStack.Length;
        if (layoutStackCount >= count) {
            var newStack = new LayoutNode[2 * count];
            Array.Copy(layoutStack, 0, newStack, 0, count);
            layoutStack = newStack;
        }
        layoutStack[layoutStackCount] = new LayoutNode(direction, Cursor);
        layoutStackCount++;
    }

    /// <summary>
    /// Note: Internal state-only pop. Must only be invoked via <see cref="GuiWidget.PopLayout"/>
    /// to ensure the parent layout cursor is advanced with spacing.
    /// </summary>
    internal Vector2 PopLayout()
    {
        if (layoutStackCount <= 1) throw new InvalidOperationException();
        var finishedLayout  = layoutStack[--layoutStackCount];
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
        CurrentLayoutRef.cursor = value;
    }

    internal void SetTopWindow()
    {
        host.SetTopWindow(this);
    }
    
    public bool IsHoverAt(Vector2 pos, Vector2 size, ImDraw draw)
    {
        var widgetRect = new RectVector2(pos, size);
        
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

    public bool IsHoverAtCursor(Vector2 size, ImDraw draw)
    {
        return IsHoverAt(Cursor, size, draw);
    }
    
#region resize

    internal bool ProcessResize(in GuiWidget drawGui, int resizeId, float border = 15f)
    {
        var input = drawGui.input;
        var hoverEdge       = GetResizeEdge(input.MousePos, border);
        var activeEdge      = activeResizeEdge;
        var isHoverOrActive = hoverEdge != ResizeEdge.None || activeEdge != ResizeEdge.None;
        var edgeDragState   = drawGui.GetDragState(isHoverOrActive, resizeId);
        
        if (edgeDragState == DragState.Down) {
            if (activeEdge == ResizeEdge.None) {
                activeResizeEdge    = GetResizeEdge(input.MousePos, border);
                activeResizeSize    = Size;
                SetTopWindow();
            }
            var offset = input.MousePos - input.DragPosStart;
            ApplyResize(offset, activeEdge);

            input.SetCursor(GetCursorForEdge(activeEdge));
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

    private ResizeEdge GetResizeEdge(Vector2 mousePos, float margin)
    {
        if (mousePos.X < Pos.X - margin || mousePos.X > Pos.X + Size.X + margin ||
            mousePos.Y < Pos.Y - margin || mousePos.Y > Pos.Y + Size.Y + margin)
        {
            return ResizeEdge.None;
        }

        ResizeEdge edge = ResizeEdge.None;

        if (mousePos.X <= Pos.X + margin)               edge |= ResizeEdge.Left;
        else if (mousePos.X >= Pos.X + Size.X - margin) edge |= ResizeEdge.Right;

        if (mousePos.Y <= Pos.Y + margin)               edge |= ResizeEdge.Top;
        else if (mousePos.Y >= Pos.Y + Size.Y - margin) edge |= ResizeEdge.Bottom;
        
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
        var newPos  = Pos;
        var newSize = Size;
        var startSize = activeResizeSize;
        if ((edge & ResizeEdge.Right) != 0) {
            newSize.X = Math.Max(minSize.X, startSize.X + offset.X);
        }
        // Horizontal: Left
        if ((edge & ResizeEdge.Left) != 0) {
            float newWidth = Math.Max(minSize.X, startSize.X - offset.X);
            newPos.X += newSize.X - newWidth;
            newSize.X = newWidth;
        }
        // Vertical: Bottom
        if ((edge & ResizeEdge.Bottom) != 0) {
            newSize.Y = Math.Max(minSize.Y, startSize.Y + offset.Y);
        }
        // Vertical: Top
        if ((edge & ResizeEdge.Top) != 0)
        {
            float newHeight = Math.Max(minSize.Y, startSize.Y - offset.Y);
            newPos.Y += newSize.Y - newHeight;
            newSize.Y = newHeight;
        }
        bounds = new RectVector2(newPos, newSize);
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
    
    public void EnsureVisibleInScrollArea(Vector2 pos, Vector2 size)
    {
        if (!host.input.JustNavigated) {
            return;
        }
        var scrollArea = CurrentScrollArea;
        if (scrollArea.childId == 0) return; // Not inside an active ScrollArea

        ref var scrollState = ref GetOrCreateScrollState(scrollArea.childId);
        float padding = 8f;

        // Check and adjust vertical scrolling (Y-Axis)
        float widgetTop = pos.Y;
        float widgetBottom = pos.Y + size.Y;
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
        float widgetLeft = pos.X;
        float widgetRight = pos.X + size.X;
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