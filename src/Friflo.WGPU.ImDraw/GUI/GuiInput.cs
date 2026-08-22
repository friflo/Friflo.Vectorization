// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Numerics;

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable RedundantJumpStatement
// ReSharper disable DuplicatedSwitchSectionBodies
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InvertIf
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable once CheckNamespace
// ReSharper disable ConvertSwitchStatementToSwitchExpression
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
namespace Friflo.WGPU.ImDraw;


public sealed class GuiInput
{
#region input state
    private             bool                isMouseDown;
    public              Vector2             MousePos        { get; private set; }
    public              Vector2             MouseDelta      { get; private set; }
    private             Vector2             mouseLast;
    
    private             bool                isTabPressed;
    private             bool                isShiftDown;
    private             bool                isReturnPressed;
    private             bool                isSpacePressed;
    private             bool                isGamepadAPressed;
    private             Vector2             arrowDirection;
    private             Vector2             gamepadDirection;
    
    private readonly    List<KeyEvent>      keyEvents           = [];
    private readonly    List<GamepadEvent>  gamepadEvents       = [];
    
    public              bool                IsMouseDown     => isMouseDown;
    public              bool                IsSubmitPressed => isSpacePressed || isReturnPressed || isGamepadAPressed;
#endregion

    public              MouseCursor         CurrentCursor   { get; private set; } = MouseCursor.Arrow;
    public              int                 FrameCount      { get; private set; }
    
#region widget state
    // Hot/Active-State-Pattern
    /// <summary> The widget currently under the mouse cursor (reset every frame) </summary>
    private             int                     hotItem;    // MUST stay private. read/write only in GetWidgetState()
    
    /// <summary> The widget currently being interacted with (persists while mouse is held down) </summary>
    private             int                     activeItem; // MUST stay private. read/write only in GetWidgetState()
    
    // --- tab / 2D array key navigation
    private readonly    List<FocusableEntry>    currentFocusables   = new(32);
    private readonly    List<FocusableEntry>    prevFocusables      = new(32);

    private             int                     focusedItem;
    private             int                     targetFocusItem;
    private             int                     focusableCounter;
    private             int                     totalFocusablesLastFrame;
    private             int                     currentFocusIndex   = -1;
    private             int                     targetFocusIndex    = -1;
#endregion
    

    internal void SetCursor(MouseCursor cursor) {
        CurrentCursor = cursor;
    }
    
    private struct FocusableEntry {
        internal    int     id;
        internal    Vector2 center;
    }

    
    internal void AddEvent(in ImEvent ev)
    {
        switch (ev.type)
        {
            case ImEventType.MouseMotion:
            case ImEventType.MouseButtonDown:
            case ImEventType.MouseButtonUp:
                MousePos = ev.mouse;
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
            case ImEventType.KeyDown:
            case ImEventType.KeyUp:
                keyEvents.Add(ev.key);
                break;
            case ImEventType.GamepadButtonDown:
            case ImEventType.GamepadButtonUp:
                gamepadEvents.Add(ev.gamepad);
                break;
        }
    }
    
    public void SetActiveWidget(int widgetId)
    {
        activeItem = widgetId;
    }
    
    // Mutates:  widget state
    internal WidgetState GetWidgetState(bool isHover, int widgetId)
    {
        // Ignore all other widgets while another widget is currently active
        if (activeItem != 0 && activeItem != widgetId) {
            return WidgetState.None;
        }

        if (isHover) {
            if (activeItem == 0) {
                hotItem = widgetId;
            }
        } else if (hotItem == widgetId && activeItem != widgetId) {
            // Keep hotItem set while dragging outside the bounds
            hotItem = 0;
        }

        if (hotItem == widgetId && isMouseDown) {
            activeItem = widgetId;
        }

        if (activeItem == widgetId) {
            if (isMouseDown) {
               focusedItem = widgetId; 
            } else {
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
    
    
#region key navigation
    /// <summary> Single register call for both 1D (Tab) and 2D (Arrows) navigation </summary>
    // Mutates:  widget state
    internal bool RegisterFocusable(int widgetId, in Vector2 center, out bool gainedFocus)
    {
        int myIndex = focusableCounter++;
        currentFocusables.Add(new FocusableEntry { id = widgetId, center = center });
        gainedFocus = false;

        // Handle 1D Tab focus
        if (myIndex == targetFocusIndex)
        {
            focusedItem = widgetId;
            currentFocusIndex = myIndex;
            targetFocusIndex = -1;
            gainedFocus = true;
        }

        // Handle 2D Arrow focus
        if (targetFocusItem == widgetId)
        {
            focusedItem = widgetId;
            currentFocusIndex = myIndex;
            targetFocusItem = 0;
            gainedFocus = true;
        }

        if (focusedItem == widgetId) {
            currentFocusIndex = myIndex;
        }
        return focusedItem == widgetId;
    }

    private int FindBestSpatialCandidate(Vector2 direction)
    {
        Vector2 currentCenter = Vector2.Zero;
        bool foundCurrent = false;

        for (int i = 0; i < prevFocusables.Count; i++)
        {
            if (prevFocusables[i].id == focusedItem)
            {
                currentCenter = prevFocusables[i].center;
                foundCurrent = true;
                break;
            }
        }

        if (!foundCurrent) return 0;

        int bestId = 0;
        float bestScore = float.MaxValue;

        for (int i = 0; i < prevFocusables.Count; i++)
        {
            var candidate = prevFocusables[i];
            if (candidate.id == focusedItem) continue;

            Vector2 toCandidate = candidate.center - currentCenter;
            float dot = Vector2.Dot(Vector2.Normalize(toCandidate), direction);

            // Forward cone check (~70 deg angle)
            if (dot > 0.3f)
            {
                // Distance penalty for off-axis deviation
                float distSq = toCandidate.LengthSquared();
                float score = distSq / (dot * dot * dot);

                if (score < bestScore) {
                    bestScore = score;
                    bestId = candidate.id;
                }
            }
        }
        return bestId;
    }
#endregion

    private void HandleKeyEvents()
    {
        isTabPressed        = false;
        arrowDirection      = default;
        gamepadDirection    = default;
        isReturnPressed     = false;
        isSpacePressed      = false;
        isGamepadAPressed   = false;
        
        // --- gamepad events
        foreach (var gamepadEvent in gamepadEvents)
        {
            if (gamepadEvent.isDown) {
                switch (gamepadEvent.button) {
                    case ImGamepadButton.DPadRight: gamepadDirection.X = +1;    continue;
                    case ImGamepadButton.DPadLeft:  gamepadDirection.X = -1;    continue;
                    case ImGamepadButton.DPadDown:  gamepadDirection.Y = +1;    continue;
                    case ImGamepadButton.DPadUp:    gamepadDirection.Y = -1;    continue;
                    case ImGamepadButton.South:     isGamepadAPressed = true;   continue;
                }
                continue;
            }
            switch (gamepadEvent.button) {
                case ImGamepadButton.DPadRight: gamepadDirection.X = 0;     continue;
                case ImGamepadButton.DPadLeft:  gamepadDirection.X = 0;     continue;
                case ImGamepadButton.DPadUp:    gamepadDirection.Y = 0;     continue;
                case ImGamepadButton.DPadDown:  gamepadDirection.Y = 0;     continue;
            }
        }
        gamepadEvents.Clear();
        
        // --- keyboard events
        foreach (var keyEvent in keyEvents)
        {
            if (!keyEvent.isDown) {
                continue;
            }
            switch (keyEvent.code)
            {
                case KeyCode.Tab:
                    isTabPressed = true;
                    isShiftDown  = (keyEvent.mod & KeyMod.Shift) != 0;
                    break;
                case KeyCode.Left:      arrowDirection.X = -1;    break;
                case KeyCode.Right:     arrowDirection.X = +1;    break;    
                case KeyCode.Up:        arrowDirection.Y = -1;    break;
                case KeyCode.Down:      arrowDirection.Y = +1;    break;
                //
                case KeyCode.Space:     isSpacePressed  = true;   break;
                case KeyCode.Return:    isReturnPressed = true;   break;
            }
        }
        keyEvents.Clear();
    }
    
    internal void NewFrame()
    {
        FrameCount++;
        CurrentCursor = MouseCursor.Arrow;
        
        MouseDelta  = MousePos - mouseLast;
        mouseLast   = MousePos;
        
        HandleKeyEvents();
        
        
        // --- tab / 2D array key navigation ---
        
        // Save focusable count from previous frame
        totalFocusablesLastFrame = focusableCounter;
        focusableCounter = 0;

        // Swap buffer for spatial queries
        prevFocusables.Clear();
        prevFocusables.AddRange(currentFocusables);
        currentFocusables.Clear();

        // 1D Navigation (Tab)
        if (isTabPressed)
        {
            int dir = isShiftDown ? -1 : 1;
            if (totalFocusablesLastFrame > 0)
            {
                int start = currentFocusIndex < 0 ? 0 : currentFocusIndex;
                targetFocusIndex = (start + dir + totalFocusablesLastFrame) % totalFocusablesLastFrame;
            }
        }
        // 2D Navigation (Arrow keys)
        else
        {
            Vector2 dir = arrowDirection + gamepadDirection;
            if (dir != Vector2.Zero && focusedItem != 0)
            {
                targetFocusItem = FindBestSpatialCandidate(dir);
            }
        }
    }
}
