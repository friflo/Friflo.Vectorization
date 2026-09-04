// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;
// ReSharper disable InconsistentNaming


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.TUI;


public enum TuiColorMode
{
    Monochrome,
    RGB24
}

public class TuiSession
{
    private readonly    TuiBackend      backend;
    private readonly    TuiBatch        batch;
    private readonly    TestScreen      screen          = new();
    private readonly    byte[]          sendBuffer      = new byte[10000];
    private             int             sendBufferCount;
    private             TuiColorMode    colorMode       = TuiColorMode.RGB24;
    
    private static readonly byte[] ClearScreen = "\x1b[2J\x1b[H"u8.ToArray();
    
    public TuiSession()
    {
        backend = new TuiBackend();
        batch   = backend.CreateBatch();
    }
    
    public Memory<byte> IterateTui()
    {
        backend.NewFrame();
        var gui = batch.BeginGui(1280, 1000);
        
        using (gui.BeginWindow("Window 1", new Vector2(200, 200), new Vector2(600, 950))) {
            screen.Window1(gui);
        }
        sendBufferCount = 0;
        
        // clear screen
        AppendSpan(ClearScreen);
        
        AppendFrameBuffer(50, 25);
        
        return sendBuffer.AsMemory(0, sendBufferCount);
    }
    
    private void AppendFrameBuffer(int width, int height)
    {
        var start   = sendBufferCount;
        var buffer  = sendBuffer;
        
        // ------ Monochrome
        if (colorMode == TuiColorMode.Monochrome) {
            batch.DrawRectCommandsChar (width, height);
            var chars  = backend.FrameBuffer;
            for (int i = 0; i < chars.Length; i++) {
                buffer[start + i] = (byte)chars[i];
            }
            sendBufferCount += chars.Length;
            return;
        }
        
        // ------ RGB24
        // color / background are only send if changed 
        var color       = new Color32();
        var background  = new Color32();
        batch.DrawRectCommandsColor(width, height);
        var cells = backend.ColorCells;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var cell = cells[y * width + x];
                var c = cell.character; // char
                var r = cell.color.R;   // byte
                var g = cell.color.G;
                var b = cell.color.B;
                var backgroundR = cell.background.R;
                var backgroundG = cell.background.G;
                var backgroundB = cell.background.B;
                // todo implement filling buffer with color background and character
                AppendByte((byte)c);
            }
            AppendSpan("\r\n"u8);
        }
    }
    
    private void AppendByte(byte value)
    {
        sendBuffer[sendBufferCount++] = value; 
    }
    
    private void AppendSpan(ReadOnlySpan<byte> buffer)
    {
        buffer.CopyTo(sendBuffer.AsSpan(sendBufferCount, buffer.Length));
        sendBufferCount += buffer.Length;
    }

    public Memory<byte> ProcessInput(ReadOnlySpan<byte> input)
    {
        if (input.Length == 1)
        {
            if (input[0] == 0x09) { // Tab Key
                var key = new KeyEvent { code = KeyCode.Tab, isDown = true };
                backend.AddEvent(new TmEvent(TmEventType.KeyDown, key));
            }
        }
        if (input.Length >= 3 && input[0] == 0x1B && input[1] == 0x5B)
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