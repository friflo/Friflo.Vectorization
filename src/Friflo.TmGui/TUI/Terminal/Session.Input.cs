// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;


// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.TUI.Terminal;


public sealed partial class TuiSession
{
    public Memory<byte> ProcessInput(ReadOnlySpan<byte> input)
    {
        if (input.Length == 1)
        {
            switch (input[0])
            {
                case 0x09: {    // Tab
                    var key = new KeyEvent { code = KeyCode.Tab, isDown = true };
                    backend.AddEvent(new TmEvent(TmEventType.KeyDown, key));
                    break;
                }
                case 0x0D: {    // Enter
                    backend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Return, isDown = true }));
                    break;
                }
                case 0x20: {    // Space
                    backend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Space,  isDown = true }));
                    break;
                }
            }
        }
        if (input.Length >= 3 && input[0] == Escape.ESC && input[1] == Escape.CSI)
        {
            switch (input[2])
            {
                case 0x41: {    // Arrow Up
                    backend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Up,     isDown = true }));
                    break;
                }
                case 0x42: {    // Arrow Down
                    backend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Down,   isDown = true }));
                    break;
                }
                case 0x43: {    // Arrow Right
                    backend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Right,  isDown = true }));
                    break;
                }
                case 0x44: {    // Arrow Left
                    backend.AddEvent(new TmEvent(TmEventType.KeyDown, new KeyEvent { code = KeyCode.Left,   isDown = true }));
                    break;
                }
            }
        }
        return IterateTui();
    }
}