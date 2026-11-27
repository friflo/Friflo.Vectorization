// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

#if ANDROID

using System;
using System.Runtime.Versioning;
using Android.Views;

// ReSharper disable UsePatternMatching
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.MAUI;


public static partial class MauiCursor
{
    private static MouseCursor _mouseCursor;

    private static void SetCursor(SmGuiBackend backend, Microsoft.Maui.Controls.View targetView)
    {
        var cursor = backend.input.CurrentCursor;
        if (cursor == _mouseCursor) {
            return;
        }
        _mouseCursor = cursor;
        if (!OperatingSystem.IsAndroidVersionAtLeast(24)) {
            return;
        }
        if (targetView.Handler?.PlatformView is not View platformView) {
            return;
        }

        SetPointerIcon(platformView, cursor);
    }

    [SupportedOSPlatform("android24.0")]
    private static void SetPointerIcon(View platformView, MouseCursor cursor)
    {
        // Map SmGui cursor states to Android PointerIcon system icons.
        var iconType = cursor switch {
            MouseCursor.Arrow       => PointerIconType.Arrow,
            MouseCursor.ResizeN     => PointerIconType.VerticalDoubleArrow,
            MouseCursor.ResizeS     => PointerIconType.VerticalDoubleArrow,
            MouseCursor.ResizeE     => PointerIconType.HorizontalDoubleArrow,
            MouseCursor.ResizeW     => PointerIconType.HorizontalDoubleArrow,
            MouseCursor.ResizeNW    => PointerIconType.TopLeftDiagonalDoubleArrow,
            MouseCursor.ResizeSE    => PointerIconType.TopLeftDiagonalDoubleArrow,
            MouseCursor.ResizeNE    => PointerIconType.TopRightDiagonalDoubleArrow,
            MouseCursor.ResizeSW    => PointerIconType.TopRightDiagonalDoubleArrow,
            _                       => PointerIconType.Arrow
        };
        var context = platformView.Context;
        if (context != null) {
            platformView.PointerIcon = PointerIcon.GetSystemIcon(context, iconType);    
        }
    }
}

#endif