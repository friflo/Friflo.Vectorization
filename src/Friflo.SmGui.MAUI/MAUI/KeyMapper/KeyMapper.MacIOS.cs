// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

#if IOS || MACCATALYST

using System;
using System.Text;
using Microsoft.Maui;
using Microsoft.Maui.Handlers;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.MAUI;


internal sealed partial class KeyMapper : ContentViewHandler
{
    public KeyMapper() : base(Mapper, CommandMapper) { }

    // Replace the default platform view with one that can become first responder
    // and receive hardware key presses.
    protected override Microsoft.Maui.Platform.ContentView CreatePlatformView()
    {
        if (VirtualView is null)
            throw new InvalidOperationException($"{nameof(VirtualView)} must be set to create a ContentView");

        return new KeyContentView(() => Host)
        {
            CrossPlatformLayout = VirtualView
        };
    }

    private sealed class KeyContentView : Microsoft.Maui.Platform.ContentView
    {
        private readonly Func<SkiaGuiView?> skiaHost;

        public KeyContentView(Func<SkiaGuiView?> host) => skiaHost = host;

        // Required so the view can receive key presses.
        public override bool CanBecomeFirstResponder => true;

        // First responder changes map to Focused/Unfocused.
        public override bool BecomeFirstResponder()
        {
            var ok = base.BecomeFirstResponder();
            if (ok && skiaHost() is IView v) v.IsFocused = true;
            return ok;
        }

        public override bool ResignFirstResponder()
        {
            var ok = base.ResignFirstResponder();
            if (ok && skiaHost() is IView v) v.IsFocused = false;
            return ok;
        }

        // Touching the view grabs focus.
        public override void TouchesBegan(Foundation.NSSet touches, UIKit.UIEvent? evt)
        {
            BecomeFirstResponder();
            base.TouchesBegan(touches, evt);
        }

        // If the host handles the key, do not pass it up the responder chain.
        public override void PressesBegan(Foundation.NSSet<UIKit.UIPress> presses, UIKit.UIPressesEvent evt)
        {
            if (!Dispatch(presses, true))
                base.PressesBegan(presses, evt);
        }

        public override void PressesEnded(Foundation.NSSet<UIKit.UIPress> presses, UIKit.UIPressesEvent evt)
        {
            if (!Dispatch(presses, false))
                base.PressesEnded(presses, evt);
        }

        private bool Dispatch(Foundation.NSSet<UIKit.UIPress> presses, bool down)
        {
            var host = skiaHost();
            if (host is null) return false;

            var handled = false;
            foreach (UIKit.UIPress press in presses)
            {
                var key = press.Key;
                if (key is null) continue;

                Rune? character = !string.IsNullOrEmpty(key.Characters) &&
                    Rune.TryGetRuneAt(key.Characters, 0, out var r) ? r : null;

                handled |= host.RaiseKey(new HostKeyEventArgs
                {
                    Key = MapAppleKey(key.KeyCode),
                    Character = character,
                    IsDown = down,
                    Shift = key.ModifierFlags.HasFlag(UIKit.UIKeyModifierFlags.Shift),
                    Ctrl = key.ModifierFlags.HasFlag(UIKit.UIKeyModifierFlags.Control),
                    Alt = key.ModifierFlags.HasFlag(UIKit.UIKeyModifierFlags.Alternate)
                });
            }
            return handled;
        }

        private static KeyCode MapAppleKey(UIKit.UIKeyboardHidUsage keyCode) => keyCode switch
        {
            UIKit.UIKeyboardHidUsage.KeyboardTab => KeyCode.Tab,
            UIKit.UIKeyboardHidUsage.KeyboardReturnOrEnter or UIKit.UIKeyboardHidUsage.KeypadEnter => KeyCode.Return,
            UIKit.UIKeyboardHidUsage.KeyboardSpacebar => KeyCode.Space,
            UIKit.UIKeyboardHidUsage.KeyboardRightArrow => KeyCode.Right,
            UIKit.UIKeyboardHidUsage.KeyboardLeftArrow => KeyCode.Left,
            UIKit.UIKeyboardHidUsage.KeyboardDownArrow => KeyCode.Down,
            UIKit.UIKeyboardHidUsage.KeyboardUpArrow => KeyCode.Up,
            UIKit.UIKeyboardHidUsage.KeyboardLeftControl => KeyCode.LCtrl,
            UIKit.UIKeyboardHidUsage.KeyboardLeftShift => KeyCode.LShift,
            UIKit.UIKeyboardHidUsage.KeyboardLeftAlt => KeyCode.LAlt,
            UIKit.UIKeyboardHidUsage.KeyboardLeftGui => KeyCode.LGUI,
            UIKit.UIKeyboardHidUsage.KeyboardRightControl => KeyCode.RCtrl,
            UIKit.UIKeyboardHidUsage.KeyboardRightShift => KeyCode.RShift,
            UIKit.UIKeyboardHidUsage.KeyboardRightAlt => KeyCode.RAlt,
            UIKit.UIKeyboardHidUsage.KeyboardRightGui => KeyCode.RGUI,
            _ => default
        };
    }

}

#endif
