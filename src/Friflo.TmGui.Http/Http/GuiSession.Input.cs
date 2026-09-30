// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Numerics;
using System.Text;

// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;


public sealed partial class GuiSession
{
    private Vector2     pendingMousePos;
    private bool        isMouseMoveEvent;
    private bool        isMouseDownEvent;
    private bool        isMouseUpEvent;
    private int         button;
    
    
    public override void ProcessInput(ReadOnlySpan<byte> utf8Bytes)
    {
        Span<char> chars = stackalloc char[utf8Bytes.Length];
        int charCount = Encoding.UTF8.GetChars(utf8Bytes, chars);
        ReadOnlySpan<char> span = chars.Slice(0, charCount);

        // Max 16 key-value pairs on the stack
        Span<Range> ranges = stackalloc Range[16];
        int count = span.Split(ranges, ';', StringSplitOptions.RemoveEmptyEntries);

        isMouseMoveEvent = false;
        isMouseDownEvent = false;
        isMouseUpEvent   = false;

        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<char> pair = span[ranges[i]];

            int eqIndex = pair.IndexOf('=');
            if (eqIndex < 0) continue;

            ReadOnlySpan<char> key = pair.Slice(0, eqIndex);
            ReadOnlySpan<char> value = pair.Slice(eqIndex + 1);

            DispatchParam(key, value);
        }

        // Dispatch aggregated MouseMove event
        if (isMouseMoveEvent) {
            wsBackend.AddEvent(new TmEvent(TmEventType.MouseMotion, pendingMousePos));
        }
        if (isMouseDownEvent) {
            wsBackend.AddEvent(new TmEvent(TmEventType.MouseButtonDown, pendingMousePos));
        }
        if (isMouseUpEvent) {
            wsBackend.AddEvent(new TmEvent(TmEventType.MouseButtonUp, pendingMousePos));
        }
    }

    private void DispatchParam(ReadOnlySpan<char> key, ReadOnlySpan<char> value)
    {
        if (key is "canvasWidth" && int.TryParse(value, out int width)) {
            canvasWidth = width;
        }
        else if (key is "canvasHeight" && int.TryParse(value, out int height)) {
            canvasHeight = height;
        }
        else if (key is "evt")
        {
            switch (value) {
                case "mousemove":   isMouseMoveEvent = true;    break;
                case "mousedown":   isMouseDownEvent = true;    break;
                case "mouseup":     isMouseUpEvent   = true;    break;
            }
        }
        else if (key is "mouseX" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out pendingMousePos.X)) {
        }
        else if (key is "mouseY" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out pendingMousePos.Y)) {
        }
        else if (key is "button" && int.TryParse(value, out button)) {
        }
    }
}