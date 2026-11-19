// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Raylib_cs;
using Rl = Raylib_cs.Raylib;

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Raylib;


public static class RaylibInput
{
    private static readonly KeyboardKey[] AllKeys       = Enum.GetValues<KeyboardKey>();
    private static readonly bool[]        KeyDownStates = new bool[512];
    private static          MouseCursor   _mouseCursor;

    public static void UpdateInput(SmGuiBackend backend)
    {
        // --- mouse events
        float mouseX = Rl.GetMouseX();
        float mouseY = Rl.GetMouseY();
        backend.AddMousePositionState(mouseX, mouseY);
        backend.AddMouseButtonState(SmMouseButton.Left,   Rl.IsMouseButtonDown(MouseButton.Left));
        backend.AddMouseButtonState(SmMouseButton.Right,  Rl.IsMouseButtonDown(MouseButton.Right));
        backend.AddMouseButtonState(SmMouseButton.Middle, Rl.IsMouseButtonDown(MouseButton.Middle));
        backend.AddMouseButtonState(SmMouseButton.X1,     Rl.IsMouseButtonDown(MouseButton.Side));
        backend.AddMouseButtonState(SmMouseButton.X2,     Rl.IsMouseButtonDown(MouseButton.Extra));

        // --- keyboard events
        var keyStates = KeyDownStates;
        foreach (var key in AllKeys)
        {
            int keyIndex = (int)key;
            if ((uint)keyIndex >= (uint)keyStates.Length) {
                continue;
            }
            if (!TryMapKey(key, out KeyCode keyCode)) {
                continue;
            }
            bool isKeyDown = Rl.IsKeyDown(key);
            if (keyStates[keyIndex] == isKeyDown) {
                continue;
            }
            keyStates[keyIndex] = isKeyDown;
            var keyEvent = new KeyEvent { code = keyCode, isDown = isKeyDown };
            backend.AddEvent(new SmEvent(isKeyDown ? SmEventType.KeyDown : SmEventType.KeyUp, keyEvent));
        }
    }

    private static bool TryMapKey(KeyboardKey rlKey, out KeyCode keyCode)
    {
        if (rlKey == KeyboardKey.Null) {
            keyCode = default;
            return false;
        }
        keyCode = rlKey switch {
            KeyboardKey.Tab    => KeyCode.Tab,
            KeyboardKey.Enter  => KeyCode.Return,
            KeyboardKey.Space  => KeyCode.Space,
            KeyboardKey.Left   => KeyCode.Left,
            KeyboardKey.Right  => KeyCode.Right,
            KeyboardKey.Up     => KeyCode.Up,
            KeyboardKey.Down   => KeyCode.Down,
            _                  => default,
        };
        return keyCode != default;
    }

    public static void SetMouseCursor(SmGuiBackend backend)
    {
        var cursor = backend.input.CurrentCursor;
        if (cursor == _mouseCursor) {
            return;
        }
        _mouseCursor = cursor;

        var rlCursor = cursor switch {
            MouseCursor.Arrow    => Raylib_cs.MouseCursor.Arrow,
            MouseCursor.ResizeN  => Raylib_cs.MouseCursor.ResizeNs,
            MouseCursor.ResizeS  => Raylib_cs.MouseCursor.ResizeNs,
            MouseCursor.ResizeE  => Raylib_cs.MouseCursor.ResizeEw,
            MouseCursor.ResizeW  => Raylib_cs.MouseCursor.ResizeEw,
            MouseCursor.ResizeNW => Raylib_cs.MouseCursor.ResizeNwse,
            MouseCursor.ResizeSE => Raylib_cs.MouseCursor.ResizeNwse,
            MouseCursor.ResizeNE => Raylib_cs.MouseCursor.ResizeNesw,
            MouseCursor.ResizeSW => Raylib_cs.MouseCursor.ResizeNesw,
            _                    => Raylib_cs.MouseCursor.Arrow,
        };
        Rl.SetMouseCursor(rlCursor);
    }
}