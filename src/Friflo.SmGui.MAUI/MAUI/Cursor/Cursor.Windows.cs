// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

#if WINDOWS

using Microsoft.Maui.Controls;
using System.Reflection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
// ReSharper disable InconsistentNaming

// ReSharper disable UsePatternMatching
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.MAUI;


public static partial class MauiCursor
{
    private static MouseCursor _mouseCursor;

    // Cache WinUI InputSystemCursor instances to avoid repeated Create() calls.
    private static readonly InputSystemCursor Arrow         = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
    private static readonly InputSystemCursor ResizeNS      = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
    private static readonly InputSystemCursor ResizeWE      = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    private static readonly InputSystemCursor ResizeNWSE    = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthwestSoutheast);
    private static readonly InputSystemCursor ResizeNESW    = InputSystemCursor.Create(InputSystemCursorShape.SizeNortheastSouthwest);

    private static void SetCursor(SmGuiBackend backend, View targetView)
    {
        var cursor = backend.input.CurrentCursor;
        if (cursor == _mouseCursor) {
            return;
        }
        _mouseCursor = cursor;
        var platformView = targetView.Handler?.PlatformView as UIElement;
        if (platformView is null) {
            return;
        }

        var systemCursor = GetSystemCursor(cursor);
        SetCursor(platformView, systemCursor);
    }

    // Map SmGui cursor states to cached WinUI InputSystemCursor shapes.
    private static InputSystemCursor GetSystemCursor(MouseCursor cursor) => cursor switch {
        MouseCursor.Arrow       => Arrow,
        MouseCursor.ResizeN     => ResizeNS,
        MouseCursor.ResizeS     => ResizeNS,
        MouseCursor.ResizeE     => ResizeWE,
        MouseCursor.ResizeW     => ResizeWE,
        MouseCursor.ResizeNW    => ResizeNWSE,
        MouseCursor.ResizeSE    => ResizeNWSE,
        MouseCursor.ResizeNE    => ResizeNESW,
        MouseCursor.ResizeSW    => ResizeNESW,
        _                       => Arrow
    };

    private static void SetCursor(UIElement element, InputSystemCursor cursor)
    {
        // Fallback via reflection if PlatformView is a standard WinUI element (e.g. SKXamlCanvas).
        var prop = ProtectedCursorInfo;
        prop?.SetValue(element, cursor);
    }
    
    private static readonly PropertyInfo? ProtectedCursorInfo =
        typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.Instance | BindingFlags.NonPublic);
}

#endif