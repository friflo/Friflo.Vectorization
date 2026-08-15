// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;


// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public enum ImEventType
{
    MouseMotion,
    MouseButtonUp,
    MouseButtonDown,
    KeyDown,
    KeyUp,
}

public struct ImEvent (ImEventType type, Vector2 mouse)
{
    public ImEventType  type    = type;
    public Vector2      mouse   = mouse;
    public KeyEvent     key;
}

public enum WidgetState
{
    None,
    Clicked,
    Down,
    Hover
}

/// <summary> Same as: SDL3.SDL.Keycode </summary>
public enum KeyCode : uint {
    Tab     = 0x00000009u,
    Return  = 0x0000000du,
    Space   = 0x00000020u,
    //
    Right   = 0x4000004fu,
    Left    = 0x40000050u,
    Down    = 0x40000051u,
    Up      = 0x40000052u,
}

[Flags]
public enum KeyMod : ushort
{ 
    None    = 0x0000,
    LShift  = 0x0001,
    RShift  = 0x0002,
    Level5  = 0x0004, 
    LCtrl   = 0x0040,
    RCtrl   = 0x0080,
    LAlt    = 0x0100,
    RAlt    = 0x0200,
    LGUI    = 0x0400,
    RGUI    = 0x0800,
    Num     = 0x1000,
    Caps    = 0x2000,
    Mode    = 0x4000,
    Scroll  = 0x8000,
    Ctrl    = LCtrl  | RCtrl,
    Shift   = LShift | RShift,
    Alt     = LAlt   | RAlt,
    GUI     = LGUI   | RGUI
}

public struct KeyEvent
{
    public KeyCode  code;
    public KeyMod   mod;
    public bool     isDown;

    public override string ToString() => code.ToString();
}

public enum MouseCursor
{
    Arrow,
    ResizeNS,     // Vertical   (Top / Bottom)
    ResizeEW,     // Horizontal (Left / Right)
    ResizeNWSE,   // Diagonal   (TopLeft / BottomRight)
    ResizeNESW    // Diagonal   (TopRight / BottomLeft)
}