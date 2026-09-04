// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.TUI;


public class TuiSession
{
    private readonly    TuiBackend  backend;
    private readonly    TuiBatch    batch;
    private readonly    TestScreen  screen = new();
    private readonly    byte[]      sendBuffer = new byte[10000];
    private             int         sendBufferCount;
    
    private static readonly byte[] ClearScreen = "\x1b[2J\x1b[H"u8.ToArray();
    
    public TuiSession()
    {
        backend = new TuiBackend();
        batch   = backend.CreateBatch();
    }
    
    private void AppendSend(ReadOnlySpan<byte> buffer)
    {
        buffer.CopyTo(sendBuffer.AsSpan(sendBufferCount, buffer.Length));
        sendBufferCount += buffer.Length;
    }
    
    public Memory<byte> IterateTui()
    {
        backend.NewFrame();
        var gui = batch.BeginGui(1280, 1000);
        
        using (gui.BeginWindow("Window 1", new Vector2(200, 200), new Vector2(600, 950))) {
            screen.Window1(gui);
        }
        batch.DrawRectCommandsChar(50, 25);
        
        sendBufferCount = 0;
        
        // clear screen
        AppendSend(ClearScreen);
        
        // append TUI screen
        var buffer  = backend.FrameBuffer;
        var start   = sendBufferCount;
        for (int i = 0; i < buffer.Length; i++) {
            sendBuffer[start + i] = (byte)buffer[i];
        }
        sendBufferCount += buffer.Length;
        
        return sendBuffer.AsMemory(0, sendBufferCount);
    }

    public Memory<byte> ProcessInput(ReadOnlySpan<byte> input)
    {
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
                default:
                    return default;
            }
            return IterateTui();
        }
        return default;
    }
}