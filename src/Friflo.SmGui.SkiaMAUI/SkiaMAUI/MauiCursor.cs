// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using Microsoft.Maui.Controls;
using System.Reflection;

#if WINDOWS
    using Microsoft.UI.Input;
    using Microsoft.UI.Xaml;
#endif

// ReSharper disable UsePatternMatching
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.SkiaSharp;


/// <summary>
/// MAUI - a f*****g crazy over-engineered s**t ?!?
/// </summary>
public static class MauiCursor
{
    private static MouseCursor _mouseCursor;
#if WINDOWS
    public static void SetMouseCursor(SmGuiBackend backend, View targetView)
    {
        var cursor = backend.input.CurrentCursor;
        if (cursor == _mouseCursor) {
            return;
        }
        _mouseCursor = cursor;
        var platformView = targetView?.Handler?.PlatformView as UIElement;
        if (platformView is null) {
            return;
        }
        
        // Map SmGui cursor states to WinUI InputSystemCursor shapes.
        var systemCursor = cursor switch {
            MouseCursor.Arrow       => InputSystemCursor.Create(InputSystemCursorShape.Arrow),
            MouseCursor.ResizeN     => InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth),
            MouseCursor.ResizeS     => InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth),
            MouseCursor.ResizeE     => InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast),
            MouseCursor.ResizeW     => InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast),
            MouseCursor.ResizeNW    => InputSystemCursor.Create(InputSystemCursorShape.SizeNorthwestSoutheast),
            MouseCursor.ResizeSE    => InputSystemCursor.Create(InputSystemCursorShape.SizeNorthwestSoutheast),
            MouseCursor.ResizeNE    => InputSystemCursor.Create(InputSystemCursorShape.SizeNortheastSouthwest),
            MouseCursor.ResizeSW    => InputSystemCursor.Create(InputSystemCursorShape.SizeNortheastSouthwest),
            _                       => InputSystemCursor.Create(InputSystemCursorShape.Arrow)
        };

        SetCursor(platformView, systemCursor);
    }

    private static void SetCursor(UIElement element, InputSystemCursor cursor)
    {
        // Fallback via reflection if PlatformView is a standard WinUI element (e.g. SKXamlCanvas).
        var prop = ProtectedCursorInfo;
        prop?.SetValue(element, cursor);
    }
    
    private static readonly PropertyInfo ProtectedCursorInfo =
        typeof(UIElement).GetProperty("ProtectedCursor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    
#else
    // Platform cursor changes in MAUI require platform-specific handlers (e.g. WinUI/MacCatalyst).
    public static void SetMouseCursor(SmGuiBackend backend, View targetView) {
    }
#endif
}