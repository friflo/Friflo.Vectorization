// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Numerics;
using System.Text;

// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;


internal sealed partial class GuiSession
{
    private bool        receivedInput;
    private Vector2     pendingMousePos;
    private bool        isMouseMoveEvent;
    private bool        isMouseDownEvent;
    private bool        isMouseUpEvent;
    private bool        isWheelEvent;
    private Vector2     wheel;
    private bool        isKeyDownEvent;
    private bool        isKeyUpEvent;
    private KeyCode     keyCode;
    private int         button;
    /// RTT start time in nanoseconds
    private ulong       rttStart;
    
    
    internal override void ProcessInput(ReadOnlySpan<byte> utf8Bytes)
    {
        receivedInput = true;
        Span<char> chars = stackalloc char[utf8Bytes.Length];
        int charCount = Encoding.UTF8.GetChars(utf8Bytes, chars);
        ReadOnlySpan<char> span = chars.Slice(0, charCount);

        // Max 16 key-value pairs on the stack
        Span<Range> ranges = stackalloc Range[16];
        int count = span.Split(ranges, ';', StringSplitOptions.RemoveEmptyEntries);

        isMouseMoveEvent    = false;
        isMouseDownEvent    = false;
        isMouseUpEvent      = false;
        isWheelEvent        = false;
        isKeyDownEvent      = false;
        isKeyUpEvent        = false;
        wheel               = default;

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
        if (isKeyDownEvent) {
            wsBackend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code =  keyCode, isDown = true }));
        }
        if (isKeyUpEvent) {
            wsBackend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code =  keyCode, isDown = false }));
        }
        if (isWheelEvent) {
            wsBackend.AddEvent(new TmEvent(TmEventType.MouseWheel) { wheel = wheel });
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
                case "mousemove":   isMouseMoveEvent    = true;     break;
                case "mousedown":   isMouseDownEvent    = true;     break;
                case "mouseup":     isMouseUpEvent      = true;     break;
                case "wheel":       isWheelEvent        = true;     break;
                case "keydown":     isKeyDownEvent      = true;     break;
                case "keyup":       isKeyUpEvent        = true;     break;
            }
        }
        else if (key is "mouseX" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out pendingMousePos.X)) {
        }
        else if (key is "mouseY" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out pendingMousePos.Y)) {
        }
        else if (key is "button" && int.TryParse(value, out button)) {
        }
        else if (key is "deltaX" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out wheel.X)) {
            wheel.X *= -0.01f;
        }
        else if (key is "deltaY" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out wheel.Y)) {
            wheel.Y *= -0.01f;
        }
        else if (key is "key") {
        }
        else if (key is "code") {
            keyCode = GetCodeValue(value);
        }
        else if (key is "rttStart" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double start)) {
            // rttStart (nanoseconds): const time = (performance.timeOrigin + performance.now()) * 1e6;
            if (rttStart == 0) rttStart = (ulong)Math.Round(start);
            // LogInputLatency(start);
        }
    }
    
    private static KeyCode GetCodeValue(ReadOnlySpan<char> value)
    {
        return value switch  {
            "Enter"         => KeyCode.Return,
            "Tab"           => KeyCode.Tab,
            " "             => KeyCode.Space,
            //
            "ShiftLeft"     => KeyCode.LShift,
            "ShiftRight"    => KeyCode.RShift,
            //
            "ArrowUp"       => KeyCode.Up,
            "ArrowDown"     => KeyCode.Down,
            "ArrowLeft"     => KeyCode.Left,
            "ArrowRight"    => KeyCode.Right,
            _               => (KeyCode)0
        };
    }
    
    
    
    private static void LogInputLatency(double rttStart)
    {
        // JS
        // High precision Unix timestamp in nanoseconds (1 ms = 1,000,000 ns)
        // const time = (performance.timeOrigin + performance.now()) * 1e6;
        // socket.send(`evt=mousemove;mouseX=${x};mouseY=${y};time=${time}`);
        double unixNanos = GetCurrentUnixNanoseconds();
        
        var latency = unixNanos - rttStart;
        Console.WriteLine($"receive latency: {latency:F3} ms");
    }
    
    private static double GetCurrentUnixNanoseconds()
    {
        // 1 Tick = 100 ns
        // Multiplied by 100.0 to convert ticks to nanoseconds with sub-nanosecond precision
        return (DateTime.UtcNow.Ticks - 621355968000000000L) * 100.0;
    }
}