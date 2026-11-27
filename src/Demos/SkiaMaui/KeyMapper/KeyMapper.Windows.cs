// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

#if WINDOWS

using System.Text;
using Microsoft.Maui.Handlers;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.MAUI;


internal sealed partial class KeyMapper : ContentViewHandler
{
    // Windows: Focus() must be mapped to our "active" state,
    // because ContentPanel cannot receive native focus.
    private static readonly CommandMapper<IContentView, IContentViewHandler> FocusCommandMapper =
        new(ContentViewHandler.CommandMapper)
        {
            [nameof(IView.Focus)] = MapFocus
        };

    private static void MapFocus(IContentViewHandler handler, IContentView view, object? args)
    {
        if (handler is KeyMapper self)
            self.SetActive(true);
    }

    public KeyMapper() : base(Mapper, FocusCommandMapper) { }
    
    
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

}


#endif