// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Numerics;

// ReSharper disable InlineTemporaryVariable
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
namespace Friflo.ImGui2D;


public sealed class GuiInput
{
#region public    
    public              Vector2             MousePos        => mousePos - mouseOffset;
    public              Vector2             MousePosDelta   => mousePosDelta;
    public              bool                IsShiftDown     => isShiftDown;
    public              Vector2             MouseWheel      => mouseWheel;
    public              Vector2             DragPosStart    => drawPosStart;
    public              bool                IsMouseDown     => isMouseDown;
    
    public              bool                IsSubmitFired   => isSpaceFired || isReturnFired || isGamepadAFired;
    public              MouseCursor         CurrentCursor   { get; private set; } = MouseCursor.Arrow;
    public              int                 FrameCount      { get; private set; }
#endregion

#region input state
    private             bool                isMouseDown;
    private             Vector2             mousePos;
    internal            Vector2             mouseOffset;
    private             Vector2             mousePosLast;
    private             Vector2             mousePosDelta;
    
    private             Vector2             drawPosStart;
    private             Vector2             mouseWheel;
    private             Vector2             mouseWheelAccu;
    
    private             bool                isTabPressed;
    private             bool                isShiftDown;
    
    private             bool                isReturnDown;
    private             bool                isReturnFired;
    
    private             bool                isSpaceDown;
    private             bool                isSpaceFired;
    
    private             bool                isGamepadADown;
    private             bool                isGamepadAFired;
    
    private             Vector2             arrowDirection;
    private             Vector2             gamepadDirection;
    
    private readonly    List<KeyEvent>      keyEvents           = [];
    private readonly    List<GamepadEvent>  gamepadEvents       = [];
    
    private             bool                IsSubmitDown    => isSpaceDown  || isReturnDown  || isGamepadADown;
#endregion

    
#region widget state
    // Hot/Active-State-Pattern
    /// <summary> The widget currently under the mouse cursor (reset every frame) </summary>
    private             int                     hotItem;    // MUST stay private. read/write only in GetWidgetState()
    
    /// <summary> The widget currently being interacted with (persists while mouse is held down) </summary>
    private             int                     activeItem; // MUST stay private. read/write only in GetWidgetState()
    
    /// <summary> The widget currently being dragged (persists while mouse is held down) </summary>
    private             int                     dragItem;   // MUST stay private. read/write only in GetWidgetState()
    
    internal            bool                    actionHoverCaptured;
    internal            bool                    lastActionHoverCaptured; // support window resize if mouse near border but hovers a different window
    private             int                     focusedItem;
    private             GuiWindow?              focusedWindow;
    private             int                     targetFocusItem;
    private             int                     focusableCounter;
    private             int                     totalFocusablesLastFrame;
    private             int                     currentFocusIndex   = -1;
    private             int                     targetFocusIndex    = -1;
    internal            bool                    JustNavigated       { get; private set; }
#endregion
    

    internal void SetCursor(MouseCursor cursor) {
        CurrentCursor = cursor;
    }
    
    internal void AddEvent(in ImEvent ev)
    {
        switch (ev.type)
        {
            case ImEventType.MouseMotion:
            case ImEventType.MouseButtonDown:
            case ImEventType.MouseButtonUp:
                mousePos = ev.mouse;
                break;
            case  ImEventType.MouseWheel:
                mouseWheelAccu += ev.wheel;
                break;
        }
        switch (ev.type)
        {
            case ImEventType.MouseButtonDown:
                isMouseDown = true;
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

#region activeItem / dragItem

    internal bool IsDragActive => dragItem != 0 || activeItem != 0;

    /// <summary> Start and keep drag state for a widget without setting focus. </summary>
    // Mutates:  widget state
    internal DragState GetDragState(bool isHover, int widgetId)
    {
        // Ignore all interaction if another widget currently owns the drag state
        if ((dragItem != 0 && dragItem != widgetId) || activeItem != 0) {
            return DragState.None;
        }
        // Initiate drag when mouse is pressed over a hovered widget
        if (isHover && isMouseDown && dragItem == 0) {
            dragItem        = widgetId;
            drawPosStart    = MousePos;
        }
        // Process ongoing drag operation or handle release
        if (dragItem == widgetId) {
            if (isMouseDown) {
                // Active drag in progress (mouse button held down)
                return DragState.Down;
            }
            // Mouse button released: end drag operation
            dragItem = 0;
            return DragState.None;
        }
        // Fallback hover state when no drag is active
        if (isHover && dragItem == 0) {
            hotItem = widgetId;
            return DragState.Hover;
        }
        return DragState.None;
    }
    
    /// <summary> Start and keep drag state for a widget and set focus to widget. </summary>
    // Mutates:  widget state
    internal WidgetState GetWidgetState(bool isHover, int widgetId)
    {
        if (focusedItem == widgetId && IsSubmitDown) {
            return WidgetState.Down;
        }
        // Ignore all other widgets while another widget is currently active
        if ((activeItem != 0 && activeItem != widgetId) || dragItem != 0) {
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
               return WidgetState.Down;
            }
            activeItem = 0;
            if (hotItem == widgetId) {
                return WidgetState.Clicked;
            }
        }
        if (hotItem == widgetId) {
            return WidgetState.Hover;
        }
        return WidgetState.None;
    }
#endregion


#region key navigation
    /// <summary> Single register call for both 1D (Tab) and 2D (Arrows) navigation </summary>
    // Mutates:  widget state
    internal bool RegisterFocusable(GuiWindow window, int widgetId, Vector2 pos, Vector2 size) // , out bool gainedFocus
    {
        int myIndex = focusableCounter++;
        window.currentFocusables.Add(new FocusableEntry { id = widgetId, pos = pos + mouseOffset, size = size });
        // gainedFocus = false;

        // Handle 1D Tab focus
        if (myIndex == targetFocusIndex)
        {
            focusedItem         = widgetId;
            currentFocusIndex   = myIndex;
            targetFocusIndex    = -1;
            // gainedFocus      = true;
            JustNavigated       = true;
            window.SetTopWindow();
        }

        // Handle 2D Arrow focus
        if (targetFocusItem == widgetId)
        {
            focusedItem         = widgetId;
            currentFocusIndex   = myIndex;
            targetFocusItem     = 0;
            // gainedFocus      = true;
            JustNavigated       = true;
        }

        var isFocused = focusedItem == widgetId;
        if (isFocused) {
            focusedWindow       = window;
            currentFocusIndex   = myIndex;
        }
        return isFocused;
    }

    // Directional Axis-Aligned Bounding Box Distance & Overlap Search
    private int FindBestSpatialCandidate(Vector2 direction)
    {
        if (focusedWindow == null) {
            return 0;
        }
        FocusableEntry current  = default;
        bool foundCurrent       = false;
        var focusables          = focusedWindow.prevFocusables;
        
        for (int i = 0; i < focusables.Count; i++) {
            if (focusables[i].id == focusedItem) {
                current = focusables[i];
                foundCurrent = true;
                break;
            }
        }

        if (!foundCurrent) return 0;

        int bestId = 0;
        float bestScore = float.MaxValue;

        for (int i = 0; i < focusables.Count; i++)
        {
            var candidate = focusables[i];
            if (candidate.id == focusedItem) continue;

            float primaryDist;
            float crossOverlap;
            float crossDist = 0f;

            // --- HORIZONTAL NAVIGATION (Left / Right) ---
            if (MathF.Abs(direction.X) > 0f) {
                bool isRight = direction.X > 0f;

                // Edge-to-Edge distance on main axis
                primaryDist = isRight 
                    ? candidate.pos.X - (current.pos.X + current.size.X)
                    : current.pos.X - (candidate.pos.X + candidate.size.X);

                // Calculate overlap on Y-axis
                float overlapTop    = MathF.Max(current.pos.Y, candidate.pos.Y);
                float overlapBottom = MathF.Min(current.pos.Y + current.size.Y, candidate.pos.Y + candidate.size.Y);
                crossOverlap        = MathF.Max(0f, overlapBottom - overlapTop);

                // Edge-to-Edge distance on cross axis (if not overlapping)
                if (crossOverlap <= 0f) {
                    crossDist = candidate.pos.Y > current.pos.Y
                        ? candidate.pos.Y - (current.pos.Y + current.size.Y)
                        : current.pos.Y - (candidate.pos.Y + candidate.size.Y);
                }
            }
            // --- VERTICAL NAVIGATION (Up / Down) ---
            else {
                bool isDown = direction.Y > 0f;

                // Edge-to-Edge distance on main axis
                primaryDist = isDown 
                    ? candidate.pos.Y - (current.pos.Y + current.size.Y)
                    : current.pos.Y - (candidate.pos.Y + candidate.size.Y);

                // Calculate overlap on X-axis
                float overlapLeft   = MathF.Max(current.pos.X, candidate.pos.X);
                float overlapRight  = MathF.Min(current.pos.X + current.size.X, candidate.pos.X + candidate.size.X);
                crossOverlap        = MathF.Max(0f, overlapRight - overlapLeft);

                // Edge-to-Edge distance on cross axis (if not overlapping)
                if (crossOverlap <= 0f) {
                    crossDist = candidate.pos.X > current.pos.X
                        ? candidate.pos.X - (current.pos.X + current.size.X)
                        : current.pos.X - (candidate.pos.X + candidate.size.X);
                }
            }

            // Candidate must be in front of current widget (allow tiny negative tolerance for alignment borders)
            if (primaryDist < -2f) continue;
            primaryDist = MathF.Max(0f, primaryDist);

            // Scoring: Overlapping elements get massive priority (crossOverlap reduces score)
            // Non-overlapping elements get penalized by cross-edge distance
            float score = primaryDist + (crossDist * 3.0f) - (crossOverlap * 0.5f);

            if (score < bestScore) {
                bestScore = score;
                bestId = candidate.id;
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
        isReturnFired       = false;
        isSpaceFired        = false;
        isGamepadAFired     = false;
        
        // --- gamepad events
        foreach (var gamepadEvent in gamepadEvents)
        {
            if (gamepadEvent.isDown) {
                switch (gamepadEvent.button) {
                    case ImGamepadButton.DPadRight: gamepadDirection.X = +1;    continue;
                    case ImGamepadButton.DPadLeft:  gamepadDirection.X = -1;    continue;
                    case ImGamepadButton.DPadDown:  gamepadDirection.Y = +1;    continue;
                    case ImGamepadButton.DPadUp:    gamepadDirection.Y = -1;    continue;
                    case ImGamepadButton.South:     isGamepadAFired = true;
                                                    isGamepadADown = true;      continue;
                }
                continue;
            }
            switch (gamepadEvent.button) {
                case ImGamepadButton.DPadRight: gamepadDirection.X = 0;     continue;
                case ImGamepadButton.DPadLeft:  gamepadDirection.X = 0;     continue;
                case ImGamepadButton.DPadUp:    gamepadDirection.Y = 0;     continue;
                case ImGamepadButton.DPadDown:  gamepadDirection.Y = 0;     continue;
                case ImGamepadButton.South:     isGamepadADown = false;     continue;
            }
        }
        gamepadEvents.Clear();
        
        // --- keyboard events
        foreach (var keyEvent in keyEvents)
        {
            switch (keyEvent.code) {
                case KeyCode.LShift:    isShiftDown  = keyEvent.isDown;   break;
                case KeyCode.RShift:    isShiftDown  = keyEvent.isDown;   break;
            }
            if (!keyEvent.isDown) {
                switch (keyEvent.code)
                {
                    case KeyCode.Space:     isSpaceDown  = false;   break;
                    case KeyCode.Return:    isReturnDown = false;   break;
                }
                continue;
            }
            switch (keyEvent.code)
            {
                case KeyCode.Tab:       isTabPressed = true;        break;
                case KeyCode.Left:      arrowDirection.X = -1;      break;
                case KeyCode.Right:     arrowDirection.X = +1;      break;    
                case KeyCode.Up:        arrowDirection.Y = -1;      break;
                case KeyCode.Down:      arrowDirection.Y = +1;      break;
                //
                case KeyCode.Space:     isSpaceFired    = true;
                                        isSpaceDown     = true;     break;
                case KeyCode.Return:    isReturnFired   = true;
                                        isReturnDown    = true;     break;
            }
        }
        keyEvents.Clear();
    }
    
    internal void NewFrame()
    {
        FrameCount++;
        JustNavigated   = false;
        CurrentCursor   = MouseCursor.Arrow;
        
        mousePosDelta   = MousePos - mousePosLast;
        mousePosLast    = MousePos;
        mouseWheel      = mouseWheelAccu;
        mouseWheelAccu  = default;
        
        lastActionHoverCaptured = actionHoverCaptured;
        actionHoverCaptured     = false;
        
        HandleKeyEvents();
        
        // Save focusable count from previous frame
        totalFocusablesLastFrame = focusableCounter;
        focusableCounter = 0;

        // 1D Navigation (Tab)
        if (isTabPressed)
        {
            int dir = IsShiftDown ? -1 : 1;
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
