using System.Text;
using Friflo.SmGui;
using Microsoft.Maui.Handlers;
using SkiaMaui;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable UnusedMember.Local
// ReSharper disable CheckNamespace
namespace MyApp;

    internal sealed class KeyHandler : ContentViewHandler
    {
#if WINDOWS
        // Windows: Focus() must be mapped to our "active" state,
        // because ContentPanel cannot receive native focus.
        private static readonly CommandMapper<IContentView, IContentViewHandler> FocusCommandMapper =
            new(ContentViewHandler.CommandMapper)
            {
                [nameof(IView.Focus)] = MapFocus
            };

        private static void MapFocus(IContentViewHandler handler, IContentView view, object? args)
        {
            if (handler is KeyHandler self)
                self.SetActive(true);
        }

        public KeyHandler() : base(Mapper, FocusCommandMapper) { }
#else
        public KeyHandler() : base(Mapper, CommandMapper) { }
#endif

        private SkiaView? Host => VirtualView as SkiaView;

        // Sets IView.IsFocused on the SkiaView, which raises Focused/Unfocused.
        private void SetFocused(bool focused)
        {
            if (VirtualView is IView view && view.IsFocused != focused)
                view.IsFocused = focused;
        }

#if ANDROID
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

#elif WINDOWS
        // ContentPanel is not a Control and cannot receive focus.
        // Instead, listen on the window root and only forward keys while the
        // host is "active" (last click was inside the host, or Focus() was called).
        private Microsoft.UI.Xaml.UIElement? rootElement;
        private bool hostActive;

        private void SetActive(bool active)
        {
            if (hostActive == active) return;
            hostActive = active;
            SetFocused(active);
        }

        protected override void ConnectHandler(Microsoft.Maui.Platform.ContentPanel platformView)
        {
            base.ConnectHandler(platformView);
            platformView.Loaded += OnLoaded;
        }

        protected override void DisconnectHandler(Microsoft.Maui.Platform.ContentPanel platformView)
        {
            platformView.Loaded -= OnLoaded;
            Detach();
            base.DisconnectHandler(platformView);
        }

        private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // XamlRoot is only available once the view is loaded.
            Detach();
            rootElement = PlatformView.XamlRoot?.Content;
            if (rootElement is null) return;

            rootElement.KeyDown += OnKeyDown;
            rootElement.KeyUp += OnKeyUp;

            // handledEventsToo = true: also receive pointer events that child
            // controls have already marked as handled.
            rootElement.AddHandler(Microsoft.UI.Xaml.UIElement.PointerPressedEvent,
                new Microsoft.UI.Xaml.Input.PointerEventHandler(OnRootPointerPressed), true);
        }

        private void Detach()
        {
            if (rootElement is not null)
            {
                rootElement.KeyDown -= OnKeyDown;
                rootElement.KeyUp -= OnKeyUp;
                rootElement.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerPressedEvent,
                    new Microsoft.UI.Xaml.Input.PointerEventHandler(OnRootPointerPressed));
                rootElement = null;
            }
            SetActive(false);
        }

        // A click inside the host activates it, a click anywhere else deactivates it.
        private void OnRootPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            SetActive(e.OriginalSource is Microsoft.UI.Xaml.DependencyObject d && IsInside(d, PlatformView));
        }

        // Walks up the visual tree to check whether node is a descendant of root.
        private static bool IsInside(Microsoft.UI.Xaml.DependencyObject? node, Microsoft.UI.Xaml.DependencyObject root)
        {
            while (node is not null)
            {
                if (ReferenceEquals(node, root)) return true;
                node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node);
            }
            return false;
        }

        private void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e) => Forward(e, true);
        private void OnKeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e) => Forward(e, false);

        private void Forward(Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e, bool down)
        {
            // Ignore keys while the host is not active (e.g. user is typing in an Entry).
            if (!hostActive || Host is null) return;

            static bool IsDown(Windows.System.VirtualKey k) =>
                Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(k)
                    .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            // KeyRoutedEventArgs in WinUI does not expose a character directly;
            // map printable VirtualKey values as a best-effort fallback.
            Rune? character = null;
            if (down && !IsDown(Windows.System.VirtualKey.Control) && !IsDown(Windows.System.VirtualKey.Menu))
            {
                var vk = (int)e.Key;
                if (vk is >= 0x41 and <= 0x5A) // A-Z
                {
                    var ch = IsDown(Windows.System.VirtualKey.Shift) ? (char)vk : char.ToLowerInvariant((char)vk);
                    character = new Rune(ch);
                }
                else if (vk is >= 0x30 and <= 0x39 && !IsDown(Windows.System.VirtualKey.Shift)) // 0-9
                {
                    character = new Rune((char)vk);
                }
                else if (e.Key == Windows.System.VirtualKey.Space)
                {
                    character = new Rune(' ');
                }
            }

            if (Host.RaiseKey(new HostKeyEventArgs
            {
                Key = MapWindowsKey(e.Key),
                Character = character,
                IsDown = down,
                Shift = IsDown(Windows.System.VirtualKey.Shift),
                Ctrl = IsDown(Windows.System.VirtualKey.Control),
                Alt = IsDown(Windows.System.VirtualKey.Menu)
            }))
            {
                e.Handled = true;
            }
        }

        private static KeyCode MapWindowsKey(Windows.System.VirtualKey key) => key switch
        {
            Windows.System.VirtualKey.Tab => KeyCode.Tab,
            Windows.System.VirtualKey.Enter => KeyCode.Return,
            Windows.System.VirtualKey.Space => KeyCode.Space,
            Windows.System.VirtualKey.Right => KeyCode.Right,
            Windows.System.VirtualKey.Left => KeyCode.Left,
            Windows.System.VirtualKey.Down => KeyCode.Down,
            Windows.System.VirtualKey.Up => KeyCode.Up,
            Windows.System.VirtualKey.LeftControl => KeyCode.LCtrl,
            Windows.System.VirtualKey.LeftShift => KeyCode.LShift,
            Windows.System.VirtualKey.LeftMenu => KeyCode.LAlt,
            Windows.System.VirtualKey.LeftWindows => KeyCode.LGUI,
            Windows.System.VirtualKey.RightControl => KeyCode.RCtrl,
            Windows.System.VirtualKey.RightShift => KeyCode.RShift,
            Windows.System.VirtualKey.RightMenu => KeyCode.RAlt,
            Windows.System.VirtualKey.RightWindows => KeyCode.RGUI,
            _ => default
        };

#elif IOS || MACCATALYST
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
            private readonly Func<SkiaView?> skiaHost;

            public KeyContentView(Func<SkiaView?> host) => skiaHost = host;

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
#endif
    }



