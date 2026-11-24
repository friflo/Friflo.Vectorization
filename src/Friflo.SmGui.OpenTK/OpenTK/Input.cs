// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using MouseCursorShape = OpenTK.Windowing.Common.Input.MouseCursor;

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.OpenTK;


public static class OpenTkInput
{
    private static readonly Keys[] AllKeys          = Enum.GetValues<Keys>();
    private static readonly bool[] KeyDownStates    = new bool[512];
    
    public static void UpdateInput(SmGuiBackend backend, NativeWindow window)
    {
        // --- mouse events
        var mouse = window.MouseState;
        backend.AddMousePositionState(mouse.X, mouse.Y);
        backend.AddMouseButtonState(SmMouseButton.Left,     mouse.IsButtonDown(MouseButton.Left));
        backend.AddMouseButtonState(SmMouseButton.Right,    mouse.IsButtonDown(MouseButton.Right));
        backend.AddMouseButtonState(SmMouseButton.Middle,   mouse.IsButtonDown(MouseButton.Middle));
        backend.AddMouseButtonState(SmMouseButton.X1,       mouse.IsButtonDown(MouseButton.Button4));
        backend.AddMouseButtonState(SmMouseButton.X2,       mouse.IsButtonDown(MouseButton.Button5));
        
        // --- keyboard events
        var keyboard = window.KeyboardState;
        var keyStates = KeyDownStates;
        foreach (var key in AllKeys)
        {
            if (!TryMapKey(key, out KeyCode keyCode)) {
                continue;
            }
            var isKeyDown = keyboard.IsKeyDown(key);
            if (keyStates[(int)key] == isKeyDown) {
                continue;
            }
            keyStates[(int)key] = isKeyDown;
            var keyEvent = new KeyEvent { code = keyCode, isDown = isKeyDown };
            backend.AddEvent(new SmEvent(isKeyDown ? SmEventType.KeyDown : SmEventType.KeyUp, keyEvent));
        }
    }
    
    private static bool TryMapKey(Keys openTkKey, out KeyCode keyCode)
    {
        if (openTkKey == Keys.Unknown) {
            keyCode = default;
            return false;
        }
        keyCode = openTkKey switch {
            Keys.Tab    => KeyCode.Tab,
            Keys.Enter  => KeyCode.Return,
            Keys.Space  => KeyCode.Space,
            //
            Keys.Left   => KeyCode.Left,
            Keys.Right  => KeyCode.Right,
            Keys.Up     => KeyCode.Up,
            Keys.Down   => KeyCode.Down,

            _           => default,
        };
        return keyCode != default;
    }
    
    private static MouseCursor _mouseCursor;
    
    public static void SetMouseCursor(SmGuiBackend backend, NativeWindow window)
    {
        var cursor = backend.input.CurrentCursor;
        if (cursor == _mouseCursor) {
            return;
        }
        _mouseCursor = cursor;
            
        MouseCursorShape shape = cursor switch  {
            MouseCursor.Arrow       => MouseCursorShape.Default,
            MouseCursor.ResizeN     => MouseCursorShape.ResizeNS,
            MouseCursor.ResizeS     => MouseCursorShape.ResizeNS,
            MouseCursor.ResizeE     => MouseCursorShape.ResizeEW,
            MouseCursor.ResizeW     => MouseCursorShape.ResizeEW,
            MouseCursor.ResizeNW    => MouseCursorShape.ResizeNWSE,
            MouseCursor.ResizeSE    => MouseCursorShape.ResizeNWSE,
            MouseCursor.ResizeNE    => MouseCursorShape.ResizeNESW,
            MouseCursor.ResizeSW    => MouseCursorShape.ResizeNESW,
            _                       => MouseCursorShape.Default
        };
        window.Cursor = shape;
    }
}