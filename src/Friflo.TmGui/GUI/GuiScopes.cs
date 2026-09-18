// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Numerics;

// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


// ----------------------------------------------------------
public readonly ref struct WindowScope
{
    private  readonly   GuiWidget   widget;
    internal readonly   WindowEnd   end;

    internal WindowScope(GuiWidget widget, WindowEnd end)
    {
        this.widget = widget;
        this.end    = end;
    }

    public void Dispose() => widget.EndWindow(this);
}

internal readonly struct WindowEnd
{
    internal readonly   bool        isOpen;
    internal readonly   int         windowId;
    internal readonly   Vector2     startCursor;
    internal readonly   Vector2     outerSize;

    internal WindowEnd(bool isOpen, int windowId, Vector2 startCursor, Vector2 outerSize)
    {
        this.isOpen      = isOpen;
        this.windowId    = windowId;
        this.startCursor = startCursor;
        this.outerSize   = outerSize;
    }
}


// ----------------------------------------------------------
public readonly ref struct ScrollAreaScope
{
    private  readonly   GuiWidget       widget;
    internal readonly   ScrollAreaEnd   end;

    internal ScrollAreaScope(GuiWidget widget, ScrollAreaEnd end)
    {
        this.widget = widget;
        this.end    = end;
    }

    public void Dispose() => widget.EndScrollArea(this);
}

internal readonly struct ScrollAreaEnd
{
    internal readonly   int         childId;
    internal readonly   Vector2     startCursor;
    internal readonly   Vector2     outerSize;

    internal ScrollAreaEnd(int childId, Vector2 startCursor, Vector2 outerSize)
    {
        this.childId     = childId;
        this.startCursor = startCursor;
        this.outerSize   = outerSize;
    }
}


// ----------------------------------------------------------
public readonly ref struct ChildScope
{
    private  readonly   GuiWidget   widget;
    internal readonly   ChildEnd    end;

    internal ChildScope(GuiWidget widget, ChildEnd end)
    {
        this.widget = widget;
        this.end    = end;
    }

    public void Dispose() => widget.EndChild(this);
}

internal readonly struct ChildEnd
{
    internal readonly   Vector2     startCursor;
    internal readonly   Vector2     outerSize;
    internal readonly   Dim         requestedSize;

    internal ChildEnd(Vector2 startCursor, Vector2 outerSize, Dim requestedSize)
    {
        this.startCursor   = startCursor;
        this.outerSize     = outerSize;
        this.requestedSize = requestedSize;
    }
}


// ----------------------------------------------------------
public readonly ref struct VerticalScope
{
    private readonly GuiWidget widget;

    internal VerticalScope(GuiWidget widget)
    {
        this.widget = widget;
    }

    public void Dispose() => widget.EndVertical();
}

public readonly ref struct HorizontalScope
{
    private readonly GuiWidget widget;

    internal HorizontalScope(GuiWidget widget)
    {
        this.widget = widget;
    }

    public void Dispose() => widget.EndHorizontal();
}


// ----------------------------------------------------------
public readonly ref struct HorizontalCenterScope
{
    private  readonly   GuiWidget               widget;
    internal readonly   HorizontalCenterEnd     end;

    internal HorizontalCenterScope(GuiWidget widget, HorizontalCenterEnd end)
    {
        this.widget = widget;
        this.end    = end;
    }

    public void Dispose() => widget.EndHorizontalAligned(this);
}

internal readonly struct HorizontalCenterEnd
{
    internal readonly   Vector2     oldLayoutOffset;
    internal readonly   int         centerId;
    internal readonly   float       align;

    internal HorizontalCenterEnd(int centerId, float align, Vector2 oldLayoutOffset)
    {
        this.oldLayoutOffset = oldLayoutOffset;
        this.centerId        = centerId;
        this.align           = align;
    }
}


// ----------------------------------------------------------
public readonly ref struct StyleScope
{
    private readonly GuiWidget widget;

    internal StyleScope(GuiWidget widget)
    {
        this.widget = widget;
    }

    public void Dispose()
    {
        if (widget.IsSet) widget.PopStyle();
    }
}


// ----------------------------------------------------------
public readonly ref struct SpaceScope
{
    private  readonly   GuiWidget   widget;
    internal readonly   SpaceEnd    end;
    
    public              Vector2     pos         => end.pos;
    public              Vector2     size        => end.size;
    public              bool        isFired     => end.isFired;
    public              WidgetState widgetState => end.widgetState;
           

    internal SpaceScope(GuiWidget widget, SpaceEnd end)
    {
        this.widget = widget;
        this.end    = end;
    }

    public void Dispose() => widget.EndSpace(this);
}

internal readonly struct SpaceEnd
{
    internal readonly   Vector2         pos;
    internal readonly   Vector2         size;
    internal readonly   bool            isFired;
    internal readonly   bool            isFocused;
    internal readonly   WidgetState     widgetState;

    internal SpaceEnd(Vector2 pos, Vector2 size, bool isFired, bool isFocused, WidgetState widgetState)
    {
        this.pos         = pos;
        this.size        = size;
        this.isFired     = isFired;
        this.isFocused   = isFocused;
        this.widgetState = widgetState;
    }
}