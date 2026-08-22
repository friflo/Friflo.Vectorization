// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public readonly ref struct WindowScope(GuiWidget gui, bool isOpen)
{
    private readonly GuiWidget    gui     = gui;
    private readonly bool       isOpen  = isOpen;

    public void Dispose() => gui.EndWindow();
}


public readonly ref struct VerticalScope(GuiWidget gui)
{
    private readonly GuiWidget gui = gui;

    public void Dispose() => gui.EndVertical();
}

public readonly ref struct HorizontalScope(GuiWidget gui)
{
    private readonly GuiWidget gui = gui;

    public void Dispose() => gui.EndHorizontal();
}


public readonly ref struct StyleScope(GuiWidget gui)
{
    private readonly GuiWidget gui = gui;

    public void Dispose() => gui.PopStyle();
}