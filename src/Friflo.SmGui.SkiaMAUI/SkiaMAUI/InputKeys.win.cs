// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

#if WINDOWS

using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.SkiaMAUI;


public static class MauiInputKeys
{
    private static readonly Dictionary<int, bool> KeyDownStates = new();
    private static SmGuiBackend? _backend;
    private static UIElement? _hookedElement;

    public static void Attach(View targetView, SmGuiBackend backend)
    {
        // Bind keyboard events once the native WinUI element is available.
        _backend = backend;
        targetView.HandlerChanged += (_, _) => HookNativeElement(targetView);
        HookNativeElement(targetView);
    }

    private static void HookNativeElement(View targetView)
    {
        if (targetView.Handler?.PlatformView is not UIElement element || element == _hookedElement) {
            return;
        }

        Unhook();
        _hookedElement = element;
        _hookedElement.IsTabStop = true;
        _hookedElement.KeyDown += OnKeyDown;
        _hookedElement.KeyUp += OnKeyUp;
    }

    public static void Unhook()
    {
        if (_hookedElement is null) {
            return;
        }

        _hookedElement.KeyDown -= OnKeyDown;
        _hookedElement.KeyUp -= OnKeyUp;
        _hookedElement = null;
    }

    private static void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        UpdateKeyState((int)e.Key, true);
    }

    private static void OnKeyUp(object sender, KeyRoutedEventArgs e)
    {
        UpdateKeyState((int)e.Key, false);
    }

    private static void UpdateKeyState(int rawKey, bool isKeyDown)
    {
        if (_backend is null || !TryMapKey(rawKey, out KeyCode keyCode)) {
            return;
        }

        KeyDownStates.TryGetValue(rawKey, out var previousDown);
        if (previousDown == isKeyDown) {
            return;
        }

        KeyDownStates[rawKey] = isKeyDown;
        var keyEvent = new KeyEvent { code = keyCode, isDown = isKeyDown };
        _backend.AddEvent(new SmEvent(isKeyDown ? SmEventType.KeyDown : SmEventType.KeyUp, keyEvent));
    }

    private static bool TryMapKey(int rawKey, out KeyCode keyCode)
    {
        var key = (VirtualKey)rawKey;
        if (key == VirtualKey.None) {
            keyCode = default;
            return false;
        }

        // Map Windows VirtualKey values to SmGui KeyCode values.
        keyCode = key switch {
            VirtualKey.Tab      => KeyCode.Tab,
            VirtualKey.Enter    => KeyCode.Return,
            VirtualKey.Space    => KeyCode.Space,
            VirtualKey.Left     => KeyCode.Left,
            VirtualKey.Right    => KeyCode.Right,
            VirtualKey.Up       => KeyCode.Up,
            VirtualKey.Down     => KeyCode.Down,
            _                   => default,
        };
        return keyCode != default;
    }
}

#endif
