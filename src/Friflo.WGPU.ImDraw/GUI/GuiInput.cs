// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;

// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable once CheckNamespace
// ReSharper disable ConvertSwitchStatementToSwitchExpression
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
namespace Friflo.WGPU.ImDraw;

public enum ImEventType
{
    MouseMotion,
    MouseButtonUp,
    MouseButtonDown,
}

public readonly struct ImEvent (ImEventType type, Vector2 mouse)
{
    public readonly ImEventType  type    = type;
    public readonly Vector2      mouse   = mouse;
}

public enum WidgetState
{
    None,
    Clicked,
    Down,
    Hover
}

public class GuiInput
{
    private     bool    isMouseDown;
    public      Vector2 Mouse       { get; private set;}
    public      Vector2 MouseDelta  { get; private set;}
    private     Vector2 mouseLast;
    
    
    // Hot/Active-State-Pattern
    /// <summary> The widget currently under the mouse cursor (reset every frame) </summary>
    private     int     hotItem;
    
    /// <summary> The widget currently being interacted with (persists while mouse is held down) </summary>
    private     int     activeItem;
    
    public void AddEvent(in ImEvent ev)
    {
        switch (ev.type)
        {
            case ImEventType.MouseMotion:
            case ImEventType.MouseButtonDown:
            case ImEventType.MouseButtonUp:
                Mouse = ev.mouse;
                break;
        }
        switch (ev.type)
        {
            case ImEventType.MouseButtonDown:
                isMouseDown     = true;
                break;
            case ImEventType.MouseButtonUp:
                isMouseDown = false;
                break;
        }
    }
    
    
    public WidgetState GetWidgetState(bool isHover, int widgetId)
    {
        if (isHover) {
            if (activeItem == 0) {
                hotItem = widgetId;
            }
        } else if (hotItem == widgetId) {
            hotItem = 0;
        }

        if (hotItem == widgetId && isMouseDown) {
            activeItem = widgetId;
        }

        if (activeItem == widgetId) {
            if (!isMouseDown) {
                activeItem = 0;
                if (hotItem == widgetId) {
                    return WidgetState.Clicked;
                }
            }
        }

        if (activeItem == widgetId) {
            return WidgetState.Down;
        }
        if (hotItem == widgetId) {
            return WidgetState.Hover;
        }
        return WidgetState.None;
    }

    public void NewFrame()
    {
        MouseDelta = Mouse - mouseLast;
        mouseLast = Mouse;
    }
}
