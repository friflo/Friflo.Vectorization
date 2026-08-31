// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Numerics;

// ReSharper disable once CheckNamespace
namespace Friflo.ImGui2D;


public readonly ref struct WindowScope(GuiWidget widget, bool isOpen)
{
    private readonly GuiWidget  widget     = widget;
    private readonly bool       isOpen  = isOpen;

    public void Dispose() => widget.EndWindow();
}

public readonly ref struct ScrollAreaScope
{
    private  readonly   GuiWidget 	widget;
    internal readonly   int         childId;
    internal readonly   Vector2 	parentStartCursor;
    internal readonly   Dim         requestedSize;
    internal readonly   Vector2     calculatedOuterSize;

    internal ScrollAreaScope(GuiWidget widget, int childId, Vector2 parentStartCursor, Dim requestedSize, Vector2 calculatedOuterSize)
    {
        this.widget                 = widget;
        this.childId                = childId;
        this.parentStartCursor      = parentStartCursor;
        this.requestedSize          = requestedSize;
        this.calculatedOuterSize    = calculatedOuterSize;
    }

    public void Dispose() => widget.EndScrollArea(this);
}

public readonly ref struct ChildScope
{
    private  readonly   GuiWidget   widget;
    internal readonly   Vector2 	parentStartCursor;
    internal readonly   Dim 	    requestedSize;
    internal readonly   Vector2     calculatedOuterSize;

    internal ChildScope(GuiWidget widget, Vector2 parentStartCursor, Dim requestedSize, Vector2 calculatedOuterSize)
    {
        this.widget              = widget;
        this.parentStartCursor   = parentStartCursor;
        this.requestedSize       = requestedSize;
        this.calculatedOuterSize = calculatedOuterSize;
    }

    public void Dispose() => widget.EndChild(this);
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

public readonly ref struct HorizontalCenterScope(GuiWidget widget, int centerId, float align, int vertexStart, Vector2 oldMouseOffset)
{
    private  readonly   GuiWidget  widget           = widget;
    internal readonly   Vector2    oldMouseOffset   = oldMouseOffset;
    internal readonly   int        centerId         = centerId;
    internal readonly   float      align            = align;
    internal readonly   int        vertexStart      = vertexStart;

    public void Dispose() => widget.EndHorizontalAligned(this);
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