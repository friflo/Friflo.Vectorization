// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using Microsoft.Maui.Controls;


// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.SkiaSharp;


public static class MauiInput
{
    private static MouseCursor _mouseCursor;

    public static void AttachPointerGestures(View targetView, SmGuiBackend backend)
    {
        // Attach pointer gestures to the MAUI view to feed SmGui mouse state.
        var pointerGesture = new PointerGestureRecognizer();

        pointerGesture.PointerMoved += (_, e) => {
            var pos = e.GetPosition(targetView);
            if (pos is { } p) {
                backend.AddMousePositionState((float)p.X, (float)p.Y);
            }
        };

        pointerGesture.PointerPressed += (_, e) => {
            var pos = e.GetPosition(targetView);
            if (pos is { } p) {
                backend.AddMousePositionState((float)p.X, (float)p.Y);
            }
            var button = MapPointerButton(e);
            backend.AddMouseButtonState(button, true);
        };

        pointerGesture.PointerReleased += (_, e) => {
            var pos = e.GetPosition(targetView);
            if (pos is { } p) {
                backend.AddMousePositionState((float)p.X, (float)p.Y);
            }
            var button = MapPointerButton(e);
            backend.AddMouseButtonState(button, false);
        };

        targetView.GestureRecognizers.Add(pointerGesture);
    }

    private static SmMouseButton MapPointerButton(PointerEventArgs e)
    {
        // Map MAUI pointer button flags to SmGui mouse buttons.
        if ((e.Button & ButtonsMask.Primary) != 0) {
            return SmMouseButton.Left;
        }
        if ((e.Button & ButtonsMask.Secondary) != 0) {
            return SmMouseButton.Right;
        }
        return default;
    }

    public static void SetMouseCursor(SmGuiBackend backend)
    {
        var cursor = backend.input.CurrentCursor;
        if (cursor == _mouseCursor) {
            return;
        }
        _mouseCursor = cursor;

        // Platform cursor changes in MAUI require platform-specific handlers (e.g. WinUI/MacCatalyst).
    }
}
