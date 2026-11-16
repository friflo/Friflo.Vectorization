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
    
    private static MouseCursor _mouseCursor;
    
    public static void SetMouseCursor(SmGuiBackend backend)
    {
        var cursor = backend.input.CurrentCursor;
        if (cursor == _mouseCursor) {
            return;
        }
        _mouseCursor = cursor;
            
        var monoCursor = cursor switch  {
            MouseCursor.Arrow       => Microsoft.Xna.Framework.Input.MouseCursor.Arrow,
            MouseCursor.ResizeN     => Microsoft.Xna.Framework.Input.MouseCursor.SizeNS,
            MouseCursor.ResizeS     => Microsoft.Xna.Framework.Input.MouseCursor.SizeNS,
            MouseCursor.ResizeE     => Microsoft.Xna.Framework.Input.MouseCursor.SizeWE,
            MouseCursor.ResizeW     => Microsoft.Xna.Framework.Input.MouseCursor.SizeWE,
            MouseCursor.ResizeNW    => Microsoft.Xna.Framework.Input.MouseCursor.SizeNWSE,
            MouseCursor.ResizeSE    => Microsoft.Xna.Framework.Input.MouseCursor.SizeNWSE,
            MouseCursor.ResizeNE    => Microsoft.Xna.Framework.Input.MouseCursor.SizeNESW,
            MouseCursor.ResizeSW    => Microsoft.Xna.Framework.Input.MouseCursor.SizeNESW,
            _                       => Microsoft.Xna.Framework.Input.MouseCursor.Arrow
        };
        Mouse.SetCursor(monoCursor);
    }
}