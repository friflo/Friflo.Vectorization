// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Numerics;

// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


// ----------------------------------------------------------
public readonly ref struct WindowScope
{
    private  readonly GuiWidget widget;
    internal readonly WindowPod pod;

    internal WindowScope(GuiWidget widget, in WindowPod pod)
    {
        this.widget = widget;
        this.pod    = pod;
    }

    public void Dispose() => widget.EndWindow(this);
}

internal readonly struct WindowPod
{
    internal readonly bool    isOpen;
    internal readonly int     windowId;
    internal readonly Vector2 startCursor;
    internal readonly Vector2 outerSize;

    internal WindowPod(bool isOpen, int windowId, Vector2 startCursor, Vector2 outerSize)
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
    private  readonly GuiWidget     widget;
    internal readonly ScrollAreaPod pod;

    internal ScrollAreaScope(GuiWidget widget, in ScrollAreaPod pod)
    {
        this.widget = widget;
        this.pod    = pod;
    }

    public void Dispose() => widget.EndScrollArea(this);
}

internal readonly struct ScrollAreaPod
{
    internal readonly int     childId;
    internal readonly Vector2 startCursor;
    internal readonly Vector2 outerSize;

    internal ScrollAreaPod(int childId, Vector2 startCursor, Vector2 outerSize)
    {
        this.childId     = childId;
        this.startCursor = startCursor;
        this.outerSize   = outerSize;
    }
}


// ----------------------------------------------------------
public readonly ref struct ChildScope
{
    private  readonly GuiWidget widget;
    internal readonly ChildPod  pod;

    internal ChildScope(GuiWidget widget, in ChildPod pod)
    {
        this.widget = widget;
        this.pod    = pod;
    }

    public void Dispose() => widget.EndChild(this);
}

internal readonly struct ChildPod
{
    internal readonly Vector2 startCursor;
    internal readonly Vector2 outerSize;
    internal readonly Dim     requestedSize;

    internal ChildPod(Vector2 startCursor, Vector2 outerSize, Dim requestedSize)
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


// ----------------------------------------------------------
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
    private  readonly GuiWidget           widget;
    internal readonly HorizontalCenterPod pod;

    internal HorizontalCenterScope(GuiWidget widget, in HorizontalCenterPod pod)
    {
        this.widget = widget;
        this.pod    = pod;
    }

    public void Dispose() => widget.EndHorizontalAligned(this);
}

internal readonly struct HorizontalCenterPod
{
    internal readonly Vector2 oldLayoutOffset;
    internal readonly int     centerId;
    internal readonly float   align;
    internal readonly int     startIndex;

    internal HorizontalCenterPod(int centerId, float align, int startIndex, Vector2 oldLayoutOffset)
    {
        this.oldLayoutOffset = oldLayoutOffset;
        this.centerId        = centerId;
        this.align           = align;
        this.startIndex      = startIndex;
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
    private  readonly   GuiWidget widget;
    internal readonly   SpacePod  pod;
    
    public              Vector2     pos         => pod.pos;
    public              Vector2     size        => pod.size;
    public              bool        isFired     => pod.isFired;
    public              WidgetState widgetState => pod.widgetState;
           

    internal SpaceScope(GuiWidget widget, in SpacePod pod)
    {
        this.widget = widget;
        this.pod    = pod;
    }

    public void Dispose() => widget.EndSpace(this);
}

internal readonly struct SpacePod
{
    internal readonly Vector2     pos;
    internal readonly Vector2     size;
    internal readonly bool        isFired;
    internal readonly bool        isFocused;
    internal readonly WidgetState widgetState;

    internal SpacePod(Vector2 pos, Vector2 size, bool isFired, bool isFocused, WidgetState widgetState)
    {
        this.pos         = pos;
        this.size        = size;
        this.isFired     = isFired;
        this.isFocused   = isFocused;
        this.widgetState = widgetState;
    }
}