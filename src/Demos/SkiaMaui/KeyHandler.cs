using System.Text;
using Microsoft.Maui.Handlers;
using SkiaMaui;

namespace MyApp;

public static class KeyHandlerExtension
{
    public static MauiAppBuilder UseKeyHandler(this MauiAppBuilder builder)
    {
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler<SkiaView, SkiaGuiHostHandler>();
        });
        return builder;
    }

    private sealed class SkiaGuiHostHandler : ContentViewHandler
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
            if (handler is SkiaGuiHostHandler self)
                self.SetActive(true);
        }

        public SkiaGuiHostHandler() : base(Mapper, FocusCommandMapper) { }
#else
        public SkiaGuiHostHandler() : base(Mapper, CommandMapper) { }
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
                Key = e.KeyCode.ToString(),
                Character = character,
                IsDown = ev.Action == Android.Views.KeyEventActions.Down,
                Shift = ev.IsShiftPressed,
                Ctrl = ev.IsCtrlPressed,
                Alt = ev.IsAltPressed
            });
        }

#elif WINDOWS
        // ContentPanel is not a Control and cannot receive focus.
        // Instead, listen on the window root and only forward keys while the
        // host is "active" (last click was inside the host, or Focus() was called).
        private Microsoft.UI.Xaml.UIElement? _root;
        private bool _active;

        private void SetActive(bool active)
        {
            if (_active == active) return;
            _active = active;
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
            _root = PlatformView.XamlRoot?.Content;
            if (_root is null) return;

            _root.KeyDown += OnKeyDown;
            _root.KeyUp += OnKeyUp;

            // handledEventsToo = true: also receive pointer events that child
            // controls have already marked as handled.
            _root.AddHandler(Microsoft.UI.Xaml.UIElement.PointerPressedEvent,
                new Microsoft.UI.Xaml.Input.PointerEventHandler(OnRootPointerPressed), true);
        }

        private void Detach()
        {
            if (_root is not null)
            {
                _root.KeyDown -= OnKeyDown;
                _root.KeyUp -= OnKeyUp;
                _root.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerPressedEvent,
                    new Microsoft.UI.Xaml.Input.PointerEventHandler(OnRootPointerPressed));
                _root = null;
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
            if (!_active || Host is null) return;

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
                    Key = e.Key.ToString(),
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
            private readonly Func<SkiaView?> _host;

            public KeyContentView(Func<SkiaView?> host) => _host = host;

            // Required so the view can receive key presses.
            public override bool CanBecomeFirstResponder => true;

            // First responder changes map to Focused/Unfocused.
            public override bool BecomeFirstResponder()
            {
                var ok = base.BecomeFirstResponder();
                if (ok && _host() is IView v) v.IsFocused = true;
                return ok;
            }

            public override bool ResignFirstResponder()
            {
                var ok = base.ResignFirstResponder();
                if (ok && _host() is IView v) v.IsFocused = false;
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
                var host = _host();
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
                        Key = key.KeyCode.ToString(),
                        Character = character,
                        IsDown = down,
                        Shift = key.ModifierFlags.HasFlag(UIKit.UIKeyModifierFlags.Shift),
                        Ctrl = key.ModifierFlags.HasFlag(UIKit.UIKeyModifierFlags.Control),
                        Alt = key.ModifierFlags.HasFlag(UIKit.UIKeyModifierFlags.Alternate)
                    });
                }
                return handled;
            }
        }
#endif
    }
}


public class HostKeyEventArgs : EventArgs
{
    /// <summary>Platform-specific key name (e.g. "Enter", "Back", "Left").</summary>
    public string Key { get; init; } = "";

    /// <summary>Character produced by the key, if the platform provides one.</summary>
    public Rune? Character { get; init; }

    public bool IsDown { get; init; }
    public bool Shift { get; init; }
    public bool Ctrl { get; init; }
    public bool Alt { get; init; }

    /// <summary>Set to true to stop the key from being passed on to the platform.</summary>
    public bool Handled { get; set; }
}