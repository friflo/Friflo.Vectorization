// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp.Views.Maui.Controls;

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.SkiaMAUI;


public static class MauiInput
{
    private static Vector2? GetPosition(SKCanvasView targetView, PointerEventArgs pointerArgs)
    {
        Point? pos = pointerArgs.GetPosition(targetView);
        if (pos is { } p) {
            return new Vector2((float)(p.X * targetView.CanvasSize.Width  / targetView.Width),
                               (float)(p.Y * targetView.CanvasSize.Height / targetView.Height));
        }
        return null;
    }

    public static void AttachPointerGestures(SKCanvasView targetView, SmGuiBackend backend)
    {
        // Attach pointer gestures to the MAUI view to feed SmGui mouse state.
        var pointerGesture = new PointerGestureRecognizer();

        pointerGesture.PointerMoved += (_, e) => {
            var pos = GetPosition(targetView, e);
            if (pos is { } p) {
                backend.AddMousePositionState(p.X, p.Y);
            }
        };

        pointerGesture.PointerPressed += (_, e) => {
            var pos = GetPosition(targetView, e);
            if (pos is { } p) {
                backend.AddMousePositionState(p.X, p.Y);
            }
            var button = MapPointerButton(e);
            backend.AddMouseButtonState(button, true);
        };

        pointerGesture.PointerReleased += (_, e) => {
            var pos = GetPosition(targetView, e);
            if (pos is { } p) {
                backend.AddMousePositionState(p.X, p.Y);
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
}
