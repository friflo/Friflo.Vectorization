// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Numerics;

// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public readonly ref struct WindowScope(GuiWidget widget, bool isOpen)
{
    private readonly GuiWidget  widget     = widget;
    private readonly bool       isOpen  = isOpen;

    public void Dispose() => widget.EndWindow();
}

public readonly ref struct ScrollAreaScope
{
    private readonly GuiWidget 	widget;
    private readonly int        childId;
    private readonly Vector2 	parentStartCursor;
    private readonly Vector2 	requestedSize;

    internal ScrollAreaScope(GuiWidget widget, int childId, Vector2 parentStartCursor, Vector2 requestedSize)
    {
        this.widget             = widget;
        this.childId            = childId;
        this.parentStartCursor  = parentStartCursor;
        this.requestedSize      = requestedSize;
    }

    public void Dispose() => widget.EndScrollArea(childId, parentStartCursor, requestedSize);
}

public readonly ref struct ChildScope
{
    private readonly GuiWidget 	widget;
    private readonly Vector2 	parentStartCursor;
    private readonly Vector2 	requestedSize;

    internal ChildScope(GuiWidget widget, Vector2 parentStartCursor, Vector2 requestedSize)
    {
        this.widget             = widget;
        this.parentStartCursor  = parentStartCursor;
        this.requestedSize      = requestedSize;
    }

    public void Dispose() => widget.EndChild(parentStartCursor, requestedSize);
}


public readonly ref struct VerticalScope(GuiWidget widget)
{
    private readonly GuiWidget widget = widget;

    public void Dispose() => widget.EndVertical();
}

public readonly ref struct HorizontalScope(GuiWidget widget)
{
    private readonly GuiWidget widget = widget;

    public void Dispose() => widget.EndHorizontal();
}


public readonly ref struct StyleScope(GuiWidget widget)
{
    private readonly GuiWidget widget = widget;

    public void Dispose() { if (widget.IsSet) widget.PopStyle(); }
}


public readonly ref struct SpaceScope(GuiWidget widget, Vector2 pos, Vector2 size, bool isFired, bool isFocused, WidgetState widgetState)
{
    private readonly    GuiWidget   widget      = widget;
    public  readonly    Vector2     pos         = pos;
    public  readonly    Vector2     size        = size;
    public  readonly    bool        isFired     = isFired;
    public  readonly    bool        isFocused   = isFocused;
    public  readonly    WidgetState widgetState = widgetState;

    public void Dispose() => widget.EndSpace(this);
}