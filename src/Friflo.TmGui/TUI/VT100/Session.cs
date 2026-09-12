// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO.Hashing;
using Friflo.TmGui.Client;

// ReSharper disable InlineTemporaryVariable
// ReSharper disable CanSimplifyStringEscapeSequence
// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.TUI.VT100;


internal sealed partial class TuiSession
{
    private readonly    TmClient        client;
    private readonly    FrameBuffer     frameBuffer;
    private readonly    TuiBackend      backend;
    private readonly    TuiBatch        batch;
    private readonly    IGuiView        guiView;
    private readonly    byte[]          sendBuffer      = new byte[30000];  // TODO grow if needed
    private             int             sendBufferCount;
    private readonly    TuiColorMode    colorMode;
    private             int             frameWidth      = 50;
    private             int             frameHeight     = 20;
    private             bool            sessionStart;
    //
    private             ulong           lastSendHash;
    private             int             sendCounter;
    
    public TuiSession(IGuiView guiView, TmClient client, FrameBuffer frameBuffer, TuiColorMode colorMode)
    {
        this.client         = client;
        this.guiView        = guiView;
        this.frameBuffer    = frameBuffer;
        this.colorMode      = colorMode;
        backend             = new TuiBackend();
        batch               = backend.CreateBatch(colorMode);
    }

    private void SetFrameSize(int width, int height)
    {
        frameWidth      = width;
        frameHeight     = height;
        lastSendHash    = 0; // force send frame
    }
    
    internal ReadOnlyMemory<byte> StartSession() {
        sessionStart = true;
        InitialCommands();
        return sendBuffer.AsMemory(0, sendBufferCount);
    }
    
    private void InitialCommands()
    {
        // Enable raw mode on client terminal
        AppendSpan(EscapeWrite.EnableRawTuiMode);
        
        AppendSpan("\x1b[?1003h"u8);    // Enable mouse hover (tracks ALL movement, clicks & scrolling)
        AppendSpan("\x1b[?1006h"u8);    // Enable SGR extended coordinate format (required for modern terminals & high resolutions)
        AppendSpan("\x1b[18t"u8);       // Request current terminal size from terminal via stdout - answer handled by HandleInBandResize()
    }
    
    private Memory<byte> IterateTui()
    {
        if (client is ConsoleClient) {
            var width   = Console.WindowWidth;
            var height  = Console.WindowHeight;
            if (frameWidth != width || frameHeight != height) {
                SetFrameSize(width, height);
            }
        }
        backend.NewFrame();
        
        // renderer gui in pixel units to support GUI & TUI with same application code
        var pixelWidth  = (int)(frameWidth  * batch.CharWidth);
        var pixelHeight = (int)(frameHeight * batch.LineHeight);
        
        guiView.RenderGui(batch, pixelWidth, pixelHeight);
        
        if (batch.guiState.scrollAreaChanged) {
            backend.NewFrame();
            guiView.RenderGui(batch, pixelWidth, pixelHeight);
            // Console.WriteLine("Scroll Area Changed");
        }
        
        if (sessionStart) {
            sessionStart = false;
        } else {
            sendBufferCount = 0;
        }
        // clear screen
        AppendSpan(EscapeWrite.ClearScreen);
        
        AppendFrameBuffer(frameWidth, frameHeight);
        
        var sendMemory  = sendBuffer.AsMemory(0, sendBufferCount);
        var sendHash    = XxHash3.HashToUInt64(sendMemory.Span);
        if (sendHash == lastSendHash) {
            return default;
        }
        sendCounter++;
        Debug.Write(sendCounter);
        Debug.WriteLine(" - send buffer");
        lastSendHash = sendHash;
        return sendMemory;
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
        var textStyle   = TextStyle.None;
        
        batch.DrawRectCommandsColor(frameBuffer, width, height, new TuiColorCell { character = ' ', background = 0x888888ff });
        
        if (backend.input.CurrentCursor != MouseCursor.Arrow) {
            DrawMouseCursor();
        }
        
        var cells = frameBuffer.ColorCells;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var cell = cells[y * width + x];
                if (cell.color.A != 0) {
                    if (cell.color != color) {
                        SetColor(color = cell.color);
                    }
                }
                if (cell.textStyle != textStyle) {
                    ApplyStyleDiff(textStyle, cell.textStyle);
                    textStyle = cell.textStyle;
                }
                if (cell.background != background) {
                    SetBackground(background = cell.background);
                }
                AppendChar(cell.character);
            }
            if (y < height - 1) {
                // Send EraseInLine + CRLF at the end of each row
                AppendSpan("\x1b[K\r\n"u8);
            }
        }
    }
    
    private void SetColor(Color32 color)
    {
        AppendSpan("\x1b[38;2;"u8);
        AppendNumber(color.R);
        AppendByte((byte)';');
        AppendNumber(color.G);
        AppendByte((byte)';');
        AppendNumber(color.B);
        AppendByte((byte)'m');
    }
    
    private void SetBackground(Color32 background)
    {
        AppendSpan("\x1b[48;2;"u8);
        AppendNumber(background.R);
        AppendByte((byte)';');
        AppendNumber(background.G);
        AppendByte((byte)';');
        AppendNumber(background.B);
        AppendByte((byte)'m');
    }

    // Allocation-free byte-to-ASCII integer formatting directly into send buffer
    private void AppendNumber(byte value)
    {
        var buffer = sendBuffer;
        if (value >= 100) {
            int d1 = value / 100;
            int rem = value % 100;
            buffer[sendBufferCount++] = (byte)('0' + d1);
            buffer[sendBufferCount++] = (byte)('0' + (rem / 10));
            buffer[sendBufferCount++] = (byte)('0' + (rem % 10));
        }
        else if (value >= 10) {
            buffer[sendBufferCount++] = (byte)('0' + (value / 10));
            buffer[sendBufferCount++] = (byte)('0' + (value % 10));
        }
        else {
            buffer[sendBufferCount++] = (byte)('0' + value);
        }
    }
    
    private void AppendByte(byte value)
    {
        sendBuffer[sendBufferCount++] = value; 
    }
    
    private void AppendChar(char character)
    {
        var buffer = sendBuffer;
        // Fast path: ASCII (1 byte) - 0x0000 to 0x007F
        if (character <= 0x7F) {
            buffer[sendBufferCount++] = (byte)character;
            return;
        }
        // UTF-8 (2 bytes) - e.g. umlauts (ä, ö, ü) or guillemets («, »)
        if (character <= 0x07FF) {
            buffer[sendBufferCount++] = (byte)(0xC0 | (character >> 6));
            buffer[sendBufferCount++] = (byte)(0x80 | (character & 0x3F));
            return;
        }
        // Skip isolated UTF-16 surrogates (4-byte characters need Rune / pair handling)
        if (char.IsSurrogate(character)) {
            return;
        }
        // UTF-8 (3 bytes) - e.g. TUI symbols (◢, ▲, ▼, ◥) and box-drawing chars
        buffer[sendBufferCount++] = (byte)(0xE0 | (character >> 12));
        buffer[sendBufferCount++] = (byte)(0x80 | ((character >> 6) & 0x3F));
        buffer[sendBufferCount++] = (byte)(0x80 | (character & 0x3F));
    }
    
    private void AppendSpan(ReadOnlySpan<byte> buffer)
    {
        buffer.CopyTo(sendBuffer.AsSpan(sendBufferCount, buffer.Length));
        sendBufferCount += buffer.Length;
    }
    
    private void ApplyStyleDiff(TextStyle oldStyle, TextStyle newStyle)
    {
        var enabled  = newStyle & ~oldStyle;
        var disabled = oldStyle & ~newStyle;

        if ((enabled & TextStyle.Bold)          != 0) AppendSpan("\x1b[1m"u8);
        if ((enabled & TextStyle.Dim)           != 0) AppendSpan("\x1b[2m"u8);
        if ((enabled & TextStyle.Italic)        != 0) AppendSpan("\x1b[3m"u8);
        if ((enabled & TextStyle.Underline)     != 0) AppendSpan("\x1b[4m"u8);
        if ((enabled & TextStyle.Inverse)       != 0) AppendSpan("\x1b[7m"u8);
        if ((enabled & TextStyle.StrikeThrough) != 0) AppendSpan("\x1b[9m"u8);

        if ((disabled & TextStyle.Bold)         != 0) AppendSpan("\x1b[22m"u8);
        if ((disabled & TextStyle.Dim)          != 0) AppendSpan("\x1b[22m"u8);
        if ((disabled & TextStyle.Italic)       != 0) AppendSpan("\x1b[23m"u8);
        if ((disabled & TextStyle.Underline)    != 0) AppendSpan("\x1b[24m"u8);
        if ((disabled & TextStyle.Inverse)      != 0) AppendSpan("\x1b[27m"u8);
        if ((disabled & TextStyle.StrikeThrough)!= 0) AppendSpan("\x1b[29m"u8);
    }
    
    private void DrawMouseCursor()
    {
        var mouse   = backend.input.MousePos; 
        var x       = (int)(mouse.X / batch.CharWidth)  - 1;
        var y       = (int)(mouse.Y / batch.LineHeight) - 1;
        
        var cell = new TuiColorCell {
            textStyle  = TextStyle.None, 
            color      = 0xffffffff,
            background = 0x606060ff
        };
        var buffer  = frameBuffer;
        var shape   = MouseCursorShape.Cursors[(int)backend.input.CurrentCursor];
        
        buffer.SetCell(x - 1, y, cell with { character = shape.left   });
        buffer.SetCell(x,     y, cell with { character = shape.center });
        buffer.SetCell(x + 1, y, cell with { character = shape.right  });
    }
}
