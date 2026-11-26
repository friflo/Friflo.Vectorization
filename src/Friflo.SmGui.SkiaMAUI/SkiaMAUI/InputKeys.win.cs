// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

#if WINDOWS

using Microsoft.Maui.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.SkiaMAUI;


public sealed class MauiInputKeys
{
    private readonly    SmGuiBackend            backend;
    private             UIElement?              hookedElement;

    public MauiInputKeys(View targetView, SmGuiBackend backend)
    {
        // Bind keyboard events once the native WinUI element is available.
        this.backend = backend;
        targetView.HandlerChanged += (_, _) => HookNativeElement(targetView);
        HookNativeElement(targetView);
    }

    private void HookNativeElement(View targetView)
    {
        if (targetView.Handler?.PlatformView is not UIElement element || element == hookedElement) {
            return;
        }
        Unhook();
        hookedElement = element;
        element.KeyDown += OnKeyDown;
        element.KeyUp   += OnKeyUp;
        element.IsTabStop = true; // required to get key input
    }

    public void Unhook()
    {
        if (hookedElement is null) {
            return;
        }
        hookedElement.KeyDown -= OnKeyDown;
        hookedElement.KeyUp -= OnKeyUp;
        hookedElement = null;
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        UpdateKeyState(e.Key, true);
    }

    private void OnKeyUp(object sender, KeyRoutedEventArgs e)
    {
        UpdateKeyState(e.Key, false);
    }

    private void UpdateKeyState(VirtualKey rawKey, bool isKeyDown)
    {
        if (!TryMapKey(rawKey, out KeyCode keyCode)) {
            return;
        }
        var keyEvent = new KeyEvent { code = keyCode, isDown = isKeyDown };
        backend.AddEvent(new SmEvent(isKeyDown ? SmEventType.KeyDown : SmEventType.KeyUp, keyEvent));
    }

    private static bool TryMapKey(VirtualKey key, out KeyCode keyCode)
    {
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
