// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;


// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.TUI.Terminal;

public sealed class TuiSession
{
    private readonly    FrameBuffer     frameBuffer;
    private readonly    TuiBackend      backend;
    private readonly    TuiBatch        batch;
    private readonly    IGuiView        guiView;
    private readonly    byte[]          sendBuffer      = new byte[10000];
    private             int             sendBufferCount;
    private readonly    TuiColorMode    colorMode;
    private             int             frameWidth      = 45;
    private             int             frameHeight     = 20;
    
    public TuiSession(IGuiView guiView, FrameBuffer frameBuffer, TuiColorMode colorMode)
    {
        this.guiView        = guiView;
        this.frameBuffer    = frameBuffer;
        this.colorMode      = colorMode;
        backend             = new TuiBackend();
        batch               = backend.CreateBatch(colorMode);
    }
    
    private Memory<byte> IterateTui()
    {
        backend.NewFrame();
        
        // renderer gui in pixel units to support GUI & TUI with same application code
        var pixelWidth  = (int)(frameWidth  * batch.CharWidth);
        var pixelHeight = (int)(frameHeight * batch.LineHeight);
        guiView.RenderGui(batch, pixelWidth, pixelHeight);

        sendBufferCount = 0;
        
        // clear screen
        AppendSpan(EscapeWrite.ClearScreen);
        
        AppendFrameBuffer(frameWidth, frameHeight);
        
        return sendBuffer.AsMemory(0, sendBufferCount);
    }
    
    private void AppendFrameBuffer(int width, int height)
    {
        var start   = sendBufferCount;
        var buffer  = sendBuffer;
        
        // ------ Monochrome
        if (colorMode == TuiColorMode.Monochrome) {
            batch.DrawRectCommandsChar (frameBuffer, width, height, ' ', "\r\n");
            var chars  = frameBuffer.CharCells;
            for (int i = 0; i < chars.Length; i++) {
                buffer[start + i] = (byte)chars[i];
            }
            sendBufferCount += chars.Length;
            return;
        }
        
        // ------ RGB24
        // color / background are only sent if changed 
        var color       = new Color32();
        var background  = new Color32();
        
        batch.DrawRectCommandsColor(frameBuffer, width, height, new TuiColorCell { character = ' ', background = 0x888888ff });
        var cells = frameBuffer.ColorCells;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var cell = cells[y * width + x];

                // Check & emit Foreground Color (38;2;R;G;B) if not transparent
                if (cell.color.A != 0 && cell.color != color) {
                    color = cell.color;
                    AppendSpan("\x1b[38;2;"u8);
                    AppendNumber(color.R);
                    AppendByte((byte)';');
                    AppendNumber(color.G);
                    AppendByte((byte)';');
                    AppendNumber(color.B);
                    AppendByte((byte)'m');
                }
                // Check & emit Background Color (48;2;R;G;B)
                if (cell.background != background) {
                    background = cell.background;
                    AppendSpan("\x1b[48;2;"u8);
                    AppendNumber(background.R);
                    AppendByte((byte)';');
                    AppendNumber(background.G);
                    AppendByte((byte)';');
                    AppendNumber(background.B);
                    AppendByte((byte)'m');
                }
                // Append Character
                AppendByte((byte)cell.character);
            }

            // Send EraseInLine + CRLF at the end of each row
            AppendSpan("\x1b[K\r\n"u8);
        }
    }

    // Allocation-free byte-to-ASCII integer formatting directly into sendBuffer
    private void AppendNumber(byte value)
    {
        if (value >= 100) {
            int d1 = value / 100;
            int rem = value % 100;
            sendBuffer[sendBufferCount++] = (byte)('0' + d1);
            sendBuffer[sendBufferCount++] = (byte)('0' + (rem / 10));
            sendBuffer[sendBufferCount++] = (byte)('0' + (rem % 10));
        }
        else if (value >= 10) {
            sendBuffer[sendBufferCount++] = (byte)('0' + (value / 10));
            sendBuffer[sendBufferCount++] = (byte)('0' + (value % 10));
        }
        else {
            sendBuffer[sendBufferCount++] = (byte)('0' + value);
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