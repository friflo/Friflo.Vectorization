// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Microsoft.Xna.Framework.Input;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.MonoGame;


public static class MonoGameInput
{
    public static void UpdateInput(SmGuiBackend backend)
    {
        var mouse = Mouse.GetState();
        backend.AddMousePositionState(mouse.X, mouse.Y);
        backend.AddMouseButtonState(SmMouseButton.Left,     mouse.LeftButton    == ButtonState.Pressed);
        backend.AddMouseButtonState(SmMouseButton.Right,    mouse.RightButton   == ButtonState.Pressed);
        backend.AddMouseButtonState(SmMouseButton.Middle,   mouse.MiddleButton  == ButtonState.Pressed);
        backend.AddMouseButtonState(SmMouseButton.X1,       mouse.XButton1      == ButtonState.Pressed);
        backend.AddMouseButtonState(SmMouseButton.X2,       mouse.XButton2      == ButtonState.Pressed);
        
        // var keyboard = Keyboard.GetState();
    }
}