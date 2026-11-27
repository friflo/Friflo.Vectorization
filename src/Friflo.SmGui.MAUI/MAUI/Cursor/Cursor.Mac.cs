// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

#if MACCATALYST

using AppKit;
using Microsoft.Maui.Controls;

// ReSharper disable UsePatternMatching
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.MAUI;


public static partial class MauiCursor
{
    private static MouseCursor _mouseCursor;

    private static void SetCursor(SmGuiBackend backend, View targetView)
    {
        var cursor = backend.input.CurrentCursor;
        if (cursor == _mouseCursor) {
            return;
        }
        _mouseCursor = cursor;
        if (targetView?.Handler?.PlatformView is null) {
            return;
        }

        // Map SmGui cursor states to AppKit NSCursor shapes on MacCatalyst.
        var nsCursor = cursor switch {
            MouseCursor.Arrow       => NSCursor.ArrowCursor,
            MouseCursor.ResizeN     => NSCursor.ResizeUpDownCursor,
            MouseCursor.ResizeS     => NSCursor.ResizeUpDownCursor,
            MouseCursor.ResizeE     => NSCursor.ResizeLeftRightCursor,
            MouseCursor.ResizeW     => NSCursor.ResizeLeftRightCursor,
            MouseCursor.ResizeNW    => NSCursor.CrosshairCursor,
            MouseCursor.ResizeSE    => NSCursor.CrosshairCursor,
            MouseCursor.ResizeNE    => NSCursor.CrosshairCursor,
            MouseCursor.ResizeSW    => NSCursor.CrosshairCursor,
            _                       => NSCursor.ArrowCursor
        };

        nsCursor.Set();
    }
}


#endif