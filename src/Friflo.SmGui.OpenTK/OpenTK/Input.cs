// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

#if REMOVE_ME

using System;
using Microsoft.Xna.Framework.Input;
using Xna = Microsoft.Xna.Framework.Input;

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.OpenTK;


public static class OpenTkInput
{
    private static readonly Keys[] AllKeys          = Enum.GetValues<Keys>();
    private static readonly bool[] KeyDownStates    = new bool[256];
    
    public static void UpdateInput(SmGuiBackend backend)
    {
        // --- mouse events
        var mouse = Mouse.GetState();
        backend.AddMousePositionState(mouse.X, mouse.Y);
        backend.AddMouseButtonState(SmMouseButton.Left,     mouse.LeftButton    == ButtonState.Pressed);
        backend.AddMouseButtonState(SmMouseButton.Right,    mouse.RightButton   == ButtonState.Pressed);
        backend.AddMouseButtonState(SmMouseButton.Middle,   mouse.MiddleButton  == ButtonState.Pressed);
        backend.AddMouseButtonState(SmMouseButton.X1,       mouse.XButton1      == ButtonState.Pressed);
        backend.AddMouseButtonState(SmMouseButton.X2,       mouse.XButton2      == ButtonState.Pressed);
        
        // --- keyboard events
        var keyboard = Keyboard.GetState();
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
    
    private static bool TryMapKey(Keys monoKey, out KeyCode keyCode)
    {
        if (monoKey == Keys.None) {
            keyCode = default;
            return false;
        }
        keyCode = monoKey switch {
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
    
    public static void SetMouseCursor(SmGuiBackend backend)
    {
        var cursor = backend.input.CurrentCursor;
        if (cursor == _mouseCursor) {
            return;
        }
        _mouseCursor = cursor;
            
        var monoCursor = cursor switch  {
            MouseCursor.Arrow       => Xna.MouseCursor.Arrow,
            MouseCursor.ResizeN     => Xna.MouseCursor.SizeNS,
            MouseCursor.ResizeS     => Xna.MouseCursor.SizeNS,
            MouseCursor.ResizeE     => Xna.MouseCursor.SizeWE,
            MouseCursor.ResizeW     => Xna.MouseCursor.SizeWE,
            MouseCursor.ResizeNW    => Xna.MouseCursor.SizeNWSE,
            MouseCursor.ResizeSE    => Xna.MouseCursor.SizeNWSE,
            MouseCursor.ResizeNE    => Xna.MouseCursor.SizeNESW,
            MouseCursor.ResizeSW    => Xna.MouseCursor.SizeNESW,
            _                       => Xna.MouseCursor.Arrow
        };
        Mouse.SetCursor(monoCursor);
    }
}

#endif