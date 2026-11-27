// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

#if ANDROID

using Android.OS;
using Android.Views;
using Microsoft.Maui.Controls;

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
        if (Build.VERSION.SdkInt < BuildVersionCodes.N) {
            return;
        }
        if (targetView?.Handler?.PlatformView is not Android.Views.View platformView) {
            return;
        }

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

        platformView.PointerIcon = PointerIcon.GetSystemIcon(platformView.Context, iconType);
    }
}

#endif