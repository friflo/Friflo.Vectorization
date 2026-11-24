// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Godot;
using CursorShape = Godot.DisplayServer.CursorShape;

// ReSharper disable MergeIntoPattern
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Godot;


public static class GodotInput
{
    public static void UpdateInput(SmGuiBackend backend, InputEvent ev)
    {
        // --- mouse events
        if (ev is InputEventMouseMotion mouseMotion) {
            backend.AddMousePositionState(mouseMotion.Position.X, mouseMotion.Position.Y);
            return;
        }
        if (ev is InputEventMouseButton mouseButton) {
            backend.AddMousePositionState(mouseButton.Position.X, mouseButton.Position.Y);
            if (TryMapMouseButton(mouseButton.ButtonIndex, out var smButton)) {
                backend.AddMouseButtonState(smButton, mouseButton.Pressed);
            }
            return;
        }
        
        // --- keyboard events
        if (ev is InputEventKey keyEvent && !keyEvent.Echo) {
            if (TryMapKey(keyEvent.Keycode, out KeyCode keyCode)) {
                var isKeyDown = keyEvent.Pressed;
                var smKeyEvent = new KeyEvent { code = keyCode, isDown = isKeyDown };
                backend.AddEvent(new SmEvent(isKeyDown ? SmEventType.KeyDown : SmEventType.KeyUp, smKeyEvent));
            }
        }
    }

    private static bool TryMapMouseButton(MouseButton buttonIndex, out SmMouseButton smButton)
    {
        smButton = buttonIndex switch {
            MouseButton.Left        => SmMouseButton.Left,
            MouseButton.Right       => SmMouseButton.Right,
            MouseButton.Middle      => SmMouseButton.Middle,
            MouseButton.Xbutton1    => SmMouseButton.X1,
            MouseButton.Xbutton2    => SmMouseButton.X2,
            _                       => default,
        };
        return buttonIndex is MouseButton.Left or MouseButton.Right or MouseButton.Middle or MouseButton.Xbutton1 or MouseButton.Xbutton2;
    }
    
    private static bool TryMapKey(Key godotKey, out KeyCode keyCode)
    {
        if (godotKey == Key.None) {
            keyCode = default;
            return false;
        }
        keyCode = godotKey switch {
            Key.Tab     => KeyCode.Tab,
            Key.Enter   => KeyCode.Return,
            Key.KpEnter => KeyCode.Return,
            Key.Space   => KeyCode.Space,
            //
            Key.Left    => KeyCode.Left,
            Key.Right   => KeyCode.Right,
            Key.Up      => KeyCode.Up,
            Key.Down    => KeyCode.Down,

            _           => default,
        };
        return keyCode != default;
    }
    
    private static MouseCursor _mouseCursor;
    
    public static void SetMouseCursor(SmGuiBackend backend)
    {
        var cursor = backend.input.CurrentCursor;
        if (cursor == _mouseCursor) {
            return;
        }
        _mouseCursor = cursor;
            
        var shape = cursor switch  {
            MouseCursor.Arrow       => CursorShape.Arrow,
            MouseCursor.ResizeN     => CursorShape.Vsize,
            MouseCursor.ResizeS     => CursorShape.Vsize,
            MouseCursor.ResizeE     => CursorShape.Hsize,
            MouseCursor.ResizeW     => CursorShape.Hsize,
            MouseCursor.ResizeNW    => CursorShape.Fdiagsize,
            MouseCursor.ResizeSE    => CursorShape.Fdiagsize,
            MouseCursor.ResizeNE    => CursorShape.Bdiagsize,
            MouseCursor.ResizeSW    => CursorShape.Bdiagsize,
            _                       => CursorShape.Arrow
        };
        DisplayServer.CursorSetShape(shape);
    }
}
