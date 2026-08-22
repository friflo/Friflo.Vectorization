// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public readonly ref struct WindowScope(GuiWidget widget, bool isOpen)
{
    private readonly GuiWidget  widget     = widget;
    private readonly bool       isOpen  = isOpen;

    public void Dispose() => widget.EndWindow();
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

    public void Dispose() => widget.PopStyle();
}