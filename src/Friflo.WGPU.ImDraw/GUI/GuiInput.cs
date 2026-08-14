// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;

public enum ImEventType
{
    MouseMotion,
    MouseButtonUp,
    MouseButtonDown,
}

public struct ImEvent
{
    public ImEventType  type;
    public float        x;
    public float        y;
}

internal class GuiInput
{
    internal    bool    isMouseDown;
    internal    float   x;
    internal    float   y;
    
    // Hot/Active-State-Pattern
    /// <summary> The widget currently under the mouse cursor (reset every frame) </summary>
    internal    int     hotItem;
    
    /// <summary> The widget currently being interacted with (persists while mouse is held down) </summary>
    internal    int     activeItem;
    
    public void AddEvent(in ImEvent ev)
    {
        switch (ev.type)
        {
            case ImEventType.MouseMotion:
                x = ev.x;
                y = ev.y;
                break;
            case ImEventType.MouseButtonDown:
                isMouseDown = true;
                break;
            case ImEventType.MouseButtonUp:
                isMouseDown = false;
                break;
        }
    }
}
