using System;
using System.Collections.Generic;
using System.Numerics;
using Friflo.SmGui;
using SDL3;

// ReSharper disable CheckNamespace
namespace Friflo.WGPU.SDL3;


internal sealed class Sdl3ClientAction : ClientAction
{
    private readonly nint window;
    
    internal Sdl3ClientAction (nint window) {
        this.window = window;
    }
        
    public override void SetDisplayMode(SmDisplayMode mode)
    {
        switch (mode) {
            case SmDisplayMode.Fullscreen:
                SDL.SetWindowFullscreen(window, true);
                break;
            case SmDisplayMode.Windowed:
                SDL.SetWindowFullscreen(window, false);
                break;
        }
    }

    public override void SetWindowTitle(ReadOnlySpan<char> value)
    {
        SDL.SetWindowTitle(window, value.ToString());
    }
    
    public override void CloseWindow()
    {
        var ev = new SDL.Event { Type = (uint)SDL.EventType.Quit };
        SDL.PushEvent(ref ev);
    }
} 

// -------- SDL3 input handling: keyboard, mouse & gamepad - required when using:  Friflo.SmGui --------
internal class Sdl3GuiInput : IDisposable
{
    private nint gamepad;

    /// <summary> Use <c> dpiScale = new Vector2(1, 1) </c> if not available. </summary>
    internal void HandleGuiInput(SmGuiBackend backend, in SDL.Event ev, Vector2 dpiScale)
    {
        var type = (SDL.EventType)ev.Type;
        switch (type)
        {
            case SDL.EventType.MouseMotion:
                var motionPos = new Vector2(dpiScale.X * ev.Motion.X, dpiScale.Y * ev.Motion.Y);
                backend.AddEvent(new SmEvent(SmEventType.MouseMotion, motionPos));
                break;
            case SDL.EventType.MouseButtonUp:
                var buttonUpPos = new Vector2(dpiScale.X * ev.Button.X, dpiScale.Y * ev.Button.Y);
                backend.AddEvent(new SmEvent(SmEventType.MouseButtonUp, buttonUpPos));
                break;
            case SDL.EventType.MouseButtonDown:
                var buttonDownPos = new Vector2(dpiScale.X * ev.Button.X, dpiScale.Y * ev.Button.Y);
                backend.AddEvent(new SmEvent(SmEventType.MouseButtonDown, buttonDownPos));
                break;
            case SDL.EventType.MouseWheel:
                backend.AddEvent(new SmEvent(SmEventType.MouseWheel) { wheel = new Vector2(ev.Wheel.X, ev.Wheel.Y) });
                break;
            case SDL.EventType.KeyDown:
                var key = new KeyEvent { code = (KeyCode)ev.Key.Key, mod = (KeyMod)ev.Key.Mod, isDown = true };
                backend.AddEvent(new SmEvent(SmEventType.KeyDown, key));
                break;
            case SDL.EventType.KeyUp:
                key = new KeyEvent { code = (KeyCode)ev.Key.Key, mod = (KeyMod)ev.Key.Mod, isDown = false };
                backend.AddEvent(new SmEvent(SmEventType.KeyUp, key));
                break;
            
            case SDL.EventType.GamepadAdded:        gamepad = SDL.OpenGamepad(ev.JDevice.Which);    break;
            case SDL.EventType.GamepadRemoved:      CloseGamepad();                                 break;
            case SDL.EventType.GamepadButtonUp:
                backend.AddEvent(new SmEvent(SmEventType.GamepadButtonUp,   (SmGamepadButton)ev.GButton.Button, false));
                break;
            case SDL.EventType.GamepadButtonDown:
                backend.AddEvent(new SmEvent(SmEventType.GamepadButtonDown, (SmGamepadButton)ev.GButton.Button, true));
                break;
        }
    }

    private void CloseGamepad()
    {
        if (gamepad == IntPtr.Zero) return;
        SDL.CloseGamepad(gamepad);
        gamepad = IntPtr.Zero;
    }

    public void Dispose()
    {
        CloseGamepad();
    }
}

/// <summary>
/// Only required to visualize window resize indicator with <see cref="Friflo.SmGui.GuiInput.CurrentCursor"/> .
/// </summary>
internal static class Sdl3Cursor
{
    private static readonly Dictionary<MouseCursor, IntPtr> CursorCache         = new();
    private static          MouseCursor                     _currentCursorType  = MouseCursor.Arrow;

    internal static void Init() {
        CursorCache[MouseCursor.Arrow]      = SDL.CreateSystemCursor(SDL.SystemCursor.Default);
        CursorCache[MouseCursor.ResizeN]    = SDL.CreateSystemCursor(SDL.SystemCursor.NSResize);
        CursorCache[MouseCursor.ResizeS]    = SDL.CreateSystemCursor(SDL.SystemCursor.NSResize);
        CursorCache[MouseCursor.ResizeE]    = SDL.CreateSystemCursor(SDL.SystemCursor.EWResize);
        CursorCache[MouseCursor.ResizeW]    = SDL.CreateSystemCursor(SDL.SystemCursor.EWResize);
        
        CursorCache[MouseCursor.ResizeNW]   = SDL.CreateSystemCursor(SDL.SystemCursor.NWSEResize);
        CursorCache[MouseCursor.ResizeSE]   = SDL.CreateSystemCursor(SDL.SystemCursor.NWSEResize);
        CursorCache[MouseCursor.ResizeNE]   = SDL.CreateSystemCursor(SDL.SystemCursor.NESWResize);
        CursorCache[MouseCursor.ResizeSW]   = SDL.CreateSystemCursor(SDL.SystemCursor.NESWResize);
    }
    
    internal static void Shutdown()
    {
        foreach (var handle in CursorCache.Values) {
            if (handle != IntPtr.Zero) SDL.DestroyCursor(handle);
        }
        CursorCache.Clear();
    }

    public static void SetCursor(MouseCursor cursor)
    {
        if (cursor == _currentCursorType) return;
        if (CursorCache.TryGetValue(cursor, out IntPtr handle)) {
            SDL.SetCursor(handle);
            _currentCursorType = cursor;
        }
    }
}