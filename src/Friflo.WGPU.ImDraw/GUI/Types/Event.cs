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
    MouseWheel,
    KeyDown,
    KeyUp,
    GamepadButtonUp,
    GamepadButtonDown,
}

public struct ImEvent
{
    public  ImEventType     type;
    public  Vector2         mouse;
    public  Vector2         wheel;
    public  KeyEvent        key;
    public  GamepadEvent    gamepad;

    public override string ToString() => type.ToString();
    
    public ImEvent(ImEventType type) {
        this.type       = type;
    }

    public ImEvent(ImEventType type, Vector2 mouse) {
        this.type    = type;
        this.mouse   = mouse;
    }
    
    public ImEvent(ImEventType type, ImGamepadButton button, bool isDown) {
        this.type       = type;
        gamepad.button  = button;
        gamepad.isDown  = isDown;
    }
    
    public ImEvent(ImEventType type, KeyEvent keyEvent) {
        this.type       = type;
        key             = keyEvent;
    }
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


/// <summary> Same enums as <c>SDL3.SDL.GamepadButton</c> </summary>
public enum ImGamepadButton
{
    Invalid = -1,
    South,          // Bottom face button (e.g. Xbox A button)
    East,           // Right face button (e.g. Xbox B button)
    West,           // Top face button (e.g. Xbox Y button) 
    North,          // Top face button (e.g. Xbox Y button) 
    Back,
    Guide,
    Start,
    LeftStick,
    RightStick,
    LeftShoulder,
    RightShoulder,
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
    Misc1,          // Additional button (e.g. Xbox Series X share button, PS5 microphone button, Nintendo Switch Pro capture button, Steam Controller QAM button, Amazon Luna microphone button, Google Stadia capture button)
    RightPaddle1,   // Upper or primary paddle, under your right hand (e.g. Xbox Elite paddle P1, DualSense Edge RB button, Right Joy-Con SR button, Steam Controller R4 button)
    LeftPaddle1,    // Upper or primary paddle, under your left hand (e.g. Xbox Elite paddle P3, DualSense Edge LB button, Left Joy-Con SL button, Steam Controller L4 button)
    RightPaddle2,   // Lower or secondary paddle, under your right hand (e.g. Xbox Elite paddle P2, DualSense Edge right Fn button, Right Joy-Con SL button, Steam Controller R5 button)
    LeftPaddle2,    // Lower or secondary paddle, under your left hand (e.g. Xbox Elite paddle P4, DualSense Edge left Fn button, Left Joy-Con SR button, Steam Controller L5 button)
    Touchpad,       // PS4/PS5 touchpad button
    
    Misc2,
    Misc3,          // Additional button (e.g. Nintendo GameCube left trigger click)
    Misc4,          // Additional button (e.g. Nintendo GameCube right trigger click)
    Misc5,
    Misc6,
    Count
}

public struct GamepadEvent
{
    public  ImGamepadButton button;
    public  bool            isDown;

    public override string ToString() => button.ToString();
}

public enum MouseCursor
{
    Arrow,
    ResizeNS,     // Vertical   (Top / Bottom)
    ResizeEW,     // Horizontal (Left / Right)
    ResizeNWSE,   // Diagonal   (TopLeft / BottomRight)
    ResizeNESW    // Diagonal   (TopRight / BottomLeft)
}