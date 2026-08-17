// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public readonly ref struct WindowScope(DrawGui gui, bool isOpen)
{
    private readonly DrawGui    gui     = gui;
    private readonly bool       isOpen  = isOpen;

    public void Dispose() => gui.EndWindow();
}


public readonly ref struct VerticalScope(DrawGui gui)
{
    private readonly DrawGui gui = gui;

    public void Dispose() => gui.EndVertical();
}

public readonly ref struct HorizontalScope(DrawGui gui)
{
    private readonly DrawGui gui = gui;

    public void Dispose() => gui.EndHorizontal();
}


public readonly ref struct StyleScope(DrawGui gui)
{
    private readonly DrawGui gui = gui;

    public void Dispose() => gui.PopStyle();
}