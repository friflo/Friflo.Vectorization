// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

#if ANDROID

using System.Text;
using Friflo.SmGui;
using Microsoft.Maui.Handlers;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable UnusedMember.Local
// ReSharper disable CheckNamespace
namespace MyApp;


internal sealed partial class KeyMapper : ContentViewHandler
{
    public KeyMapper() : base(Mapper, CommandMapper) { }

    protected override void ConnectHandler(Microsoft.Maui.Platform.ContentViewGroup platformView)
    {
        base.ConnectHandler(platformView);

        // Make the native view focusable and listen for key and focus events.
        platformView.Focusable = true;
        platformView.FocusableInTouchMode = true;
        platformView.KeyPress += OnKeyPress;
        platformView.FocusChange += OnFocusChange;
    }

    protected override void DisconnectHandler(Microsoft.Maui.Platform.ContentViewGroup platformView)
    {
        // Unsubscribe to avoid leaks and duplicate events.
        platformView.KeyPress -= OnKeyPress;
        platformView.FocusChange -= OnFocusChange;
        base.DisconnectHandler(platformView);
    }

    private void OnFocusChange(object? sender, Android.Views.View.FocusChangeEventArgs e)
        => SetFocused(e.HasFocus);

    private void OnKeyPress(object? sender, Android.Views.View.KeyEventArgs e)
    {
        var ev = e.Event;
        if (ev is null || Host is null)
        {
            e.Handled = false;
            return;
        }

        Rune? character = ev.UnicodeChar > 0 && Rune.TryCreate(ev.UnicodeChar, out var r) ? r : null;

        // Handled = true stops the key from propagating further.
        e.Handled = Host.RaiseKey(new HostKeyEventArgs
        {
            Key = MapAndroidKey(e.KeyCode),
            Character = character,
            IsDown = ev.Action == Android.Views.KeyEventActions.Down,
            Shift = ev.IsShiftPressed,
            Ctrl = ev.IsCtrlPressed,
            Alt = ev.IsAltPressed
        });
    }

    private static KeyCode MapAndroidKey(Android.Views.Keycode keyCode) => keyCode switch
    {
        Android.Views.Keycode.Tab => KeyCode.Tab,
        Android.Views.Keycode.Enter or Android.Views.Keycode.NumpadEnter => KeyCode.Return,
        Android.Views.Keycode.Space => KeyCode.Space,
        Android.Views.Keycode.DpadRight => KeyCode.Right,
        Android.Views.Keycode.DpadLeft => KeyCode.Left,
        Android.Views.Keycode.DpadDown => KeyCode.Down,
        Android.Views.Keycode.DpadUp => KeyCode.Up,
        Android.Views.Keycode.CtrlLeft => KeyCode.LCtrl,
        Android.Views.Keycode.ShiftLeft => KeyCode.LShift,
        Android.Views.Keycode.AltLeft => KeyCode.LAlt,
        Android.Views.Keycode.MetaLeft => KeyCode.LGUI,
        Android.Views.Keycode.CtrlRight => KeyCode.RCtrl,
        Android.Views.Keycode.ShiftRight => KeyCode.RShift,
        Android.Views.Keycode.AltRight => KeyCode.RAlt,
        Android.Views.Keycode.MetaRight => KeyCode.RGUI,
        _ => default
    };
}

#endif
