// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

#if MACCATALYST

using System;
using AppKit;
using Microsoft.Maui.Controls;

// ReSharper disable InconsistentNaming
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
        if (targetView.Handler?.PlatformView is null) {
            return;
        }
        // Map SmGui cursor states to AppKit NSCursor shapes on MacCatalyst.
        var nsCursor = cursor switch {
            MouseCursor.Arrow       => NSCursor.ArrowCursor,
            MouseCursor.ResizeN     => NSCursor.ResizeUpDownCursor,
            MouseCursor.ResizeS     => NSCursor.ResizeUpDownCursor,
            MouseCursor.ResizeE     => NSCursor.ResizeLeftRightCursor,
            MouseCursor.ResizeW     => NSCursor.ResizeLeftRightCursor,
            MouseCursor.ResizeNW    => ResizeNW,
            MouseCursor.ResizeSE    => ResizeSE,
            MouseCursor.ResizeNE    => ResizeNE,
            MouseCursor.ResizeSW    => ResizeSW,
            _                       => NSCursor.ArrowCursor
        };
        nsCursor.Set();
    }
    
    private static NSCursor GetNSCursor(MouseCursor cursor)
    {
        if (OperatingSystem.IsMacCatalystVersionAtLeast(18) || OperatingSystem.IsMacOSVersionAtLeast(15)) {
            return cursor switch {
                MouseCursor.ResizeNW    => NSCursor.GetFrameResizeCursor(NSCursorFrameResizePosition.TopLeft,       NSCursorFrameResizeDirections.All),
                MouseCursor.ResizeSE    => NSCursor.GetFrameResizeCursor(NSCursorFrameResizePosition.BottomRight,   NSCursorFrameResizeDirections.All),
                MouseCursor.ResizeNE    => NSCursor.GetFrameResizeCursor(NSCursorFrameResizePosition.TopRight,      NSCursorFrameResizeDirections.All),
                MouseCursor.ResizeSW    => NSCursor.GetFrameResizeCursor(NSCursorFrameResizePosition.BottomLeft,    NSCursorFrameResizeDirections.All),
                _                       => NSCursor.CrosshairCursor
            };
        }
        return NSCursor.CrosshairCursor;
    }
    
    private static readonly NSCursor ResizeNW = GetNSCursor(MouseCursor.ResizeNW);
    private static readonly NSCursor ResizeSE = GetNSCursor(MouseCursor.ResizeSE);
    private static readonly NSCursor ResizeNE = GetNSCursor(MouseCursor.ResizeNE);
    private static readonly NSCursor ResizeSW = GetNSCursor(MouseCursor.ResizeSW);
}

#endif