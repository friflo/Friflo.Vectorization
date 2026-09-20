// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using Friflo.TmGui.Session;

// ReSharper disable InlineTemporaryVariable
// ReSharper disable CanSimplifyStringEscapeSequence
// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.TUI.VT100;


internal sealed partial class TuiSession : TmSession
{
    private  readonly   TmClient        client;
    private  readonly   TuiColorMode    colorMode;
    private  readonly   FrameBuffer     frameBuffer;
    private  readonly   SixelDrawer     sixelDrawer;
    internal readonly   TuiBackend      tuiBackend;
    private  readonly   TuiBatch        tuiBatch;
    internal            IGuiView?       guiView;
    private  readonly   byte[]          sendBuffer      = new byte[60000];  // TODO grow if needed
    private             int             sendBufferCount;
    private             int             frameWidth      = 50;
    private             int             frameHeight     = 20;
    private             Vector2         cellPixelSize   = new(10, 20);
    private             bool            sessionStart;
    //
    private             ulong           lastSendHash;
    private             int             sendCounter;
    
    public TuiSession(TmClient client, FrameBuffer frameBuffer, SixelDrawer sixelDrawer, TuiColorMode colorMode)
    {
        this.client         = client;
        this.colorMode      = colorMode;
        this.frameBuffer    = frameBuffer;
        this.sixelDrawer    = sixelDrawer;
        tuiBackend          = new TuiBackend("Terminal");
        
        tuiBatch            = tuiBackend.CreateBatch(colorMode);
        tuiBatch.session    = this;
    }
    
    // --- TmSession
    internal override GuiReplay CreateReplay()
    {
        var replayBackend   = new TuiBackend("Replay");
        var replayBatch     = replayBackend.CreateBatch(colorMode);
        return new GuiReplay(replayBackend, replayBatch, this);
    }
    
    internal override void SendReplayCommands()
    {
        var replay          = tuiBatch.recorder!.replay;
        var replayBackend   = replay.backend;
        var replayBatch     = (TuiBatch)replay.batch;
        
        sendBufferCount     = 0;
        var framePayload    = RenderFrame(replayBackend, replayBatch);
        client.Send(framePayload);
    }

    // --- internal
    private void SetFrameSize(int width, int height)
    {
        frameWidth      = width;
        frameHeight     = height;
        lastSendHash    = 0; // force send frame
    }
    
    private void SetCellPixelSize(int width, int height)
    {
        cellPixelSize = new Vector2(width, height);
        tuiBatch.cellPixelScale = new Vector2(tuiBatch.CharWidth / width, tuiBatch.LineHeight / height);
    }
    
    internal ReadOnlyMemory<byte> StartSession() {
        sessionStart = true;
        InitialCommands();
        return sendBuffer.AsMemory(0, sendBufferCount);
    }
    
    private void InitialCommands()
    {
        // Extended Init Sequence:
        // \x1b[14t     = Request window pixel size (CSI 14 t)
        // \x1b[16t     = Request cell pixel size (CSI 16 t)
        // \x1b[?1l     = Normal Cursor Mode
        // \x1b[?25l    = Hide Cursor
        // \x1b[0m      = Reset All Colors/Attributes
        // \x1b[?1049h  = Switch to Alternate Screen-Buffer - Only reliable way to avoid flickering when resize to smaller screen
        // \x1b[2J      = Clear Screen
        // \x1b[3J      = Clear Scrollback-Buffer           - prevents Alternate Screen-Buffer Reflow-Ghosting
        // \x1b[?7l     = Disable Auto-Wrap
        // \x1b[H       = Home Cursor (0,0)
        AppendSpan("\x1b[14t\x1b[16t\x1b[?1l\x1b[?25l\x1b[0m\x1b[?1049h\x1b[2J\x1b[3J\x1b[?7l\x1b[H"u8);
        
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
        tuiBackend.NewFrame();
        
        // renderer gui in pixel units to support GUI & TUI with same application code
        var pixelWidth  = (int)(frameWidth  * tuiBatch.CharWidth);
        var pixelHeight = (int)(frameHeight * tuiBatch.LineHeight);
        
        guiView!.RenderGui(tuiBatch, pixelWidth, pixelHeight);
        
        if (tuiBatch.guiState.scrollAreaChanged) {
            tuiBackend.NewFrame();
            guiView.RenderGui(tuiBatch, pixelWidth, pixelHeight);
            // Console.WriteLine("Scroll Area Changed");
        }
        
        if (sessionStart) {
            sessionStart = false;
        } else {
            sendBufferCount = 0;
        }
        return RenderFrame(tuiBackend, tuiBatch);
    }
        
    private Memory<byte> RenderFrame(TmGuiBackend backend, TuiBatch batch)
    {
        // \x1b[?2026h      Sync Start (atomic frame)
        // \x1b[H           Cursor Home
        AppendSpan("\x1b[?2026h\x1b[H"u8);  // NOTE: don't use  \x1b[2J  (Clear screen)
        
        AppendFrameBuffer(backend, batch, frameWidth, frameHeight);
        
        AppendSpan("\x1b[?2026l"u8);        // Sync Stop (atomic frame)
        
        AppendSpan("\x1b[H"u8);             // Set Cursor Home Report - if user writes to console e.g. Console.WriteLine()
        
        var sendMemory  = sendBuffer.AsMemory(0, sendBufferCount);
        var sendHash    = HashUtils.XxHash3(sendMemory.Span);
        if (sendHash == lastSendHash) {
            return default;
        }
        sendCounter++;
        // Debug.Write(sendCounter); Debug.WriteLine(" - send buffer");
        lastSendHash = sendHash;
        return sendMemory;
    }
    
    private void AppendFrameBuffer(TmGuiBackend backend, TuiBatch batch, int width, int height)
    {
        // color / background are only sent if changed 
        var color       = new Color32();
        var background  = new Color32();
        var textStyle   = TextStyle.None;
        
        var clear =  new TuiColorCell { Character = ' ', color = 0x000000ff, background = 0x888888ff };
        batch.DrawRectCommands(frameBuffer, width, height, clear);
        
        if (backend.input.CurrentCursor != MouseCursor.Arrow) {
            DrawMouseCursor(backend);
        }
        
        var cells = frameBuffer.ColorCells;
        sixelDrawer.SetClipCells(cells, width, height);

        for (int y = 0; y < height; y++)
        {
            SetCursor(y + 1);
            for (int x = 0; x < width; x++)
            {
                var cell = cells[y * width + x];
                if (cell.sixelId != 0) {
                    var sixel = AppendSixel(cell.sixelId, x, y);
                    if (sixel) {
                        continue;
                    }
                }
                if (cell.color.A != 0) {
                    if ((cell.color.Packed & 0x00ffffff) != (color.Packed & 0x00ffffff)) {
                        SetColor(color = cell.color);
                    }
                }
                if (cell.textStyle != textStyle) {
                    ApplyStyleDiff(textStyle, cell.textStyle);
                    textStyle = cell.textStyle;
                }
                if ((cell.background.Packed & 0x00ffffff) != (background.Packed & 0x00ffffff)) {
                    SetBackground(background = cell.background);
                }
                AppendRune(cell.rune);
            }
            /* AppendSpan("\x1b[K"u8); // EraseInLine - erase everything right from current cursor
            if (y < height - 1) {
                AppendSpan("\r\n"u8);
            } */
        }
    }
    
    private void SetCursor(int row)
    {
        AppendSpan("\x1b["u8);
        AppendNumber((byte)row);
        AppendSpan(";1H"u8);
    }
    
    private void SetCursorPos(int row, int col)
    {
        AppendSpan("\x1b["u8);
        AppendNumber((byte)row);
        AppendSpan(";"u8);
        AppendNumber((byte)col);
        AppendSpan("H"u8);
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
    
    private void AppendRune(Rune rune)
    {
        if (rune.Value == 0) {
            return; // Skip ghost cells. They follow runes which are two cells wide like 🙂
        }
        var destination = sendBuffer.AsSpan(sendBufferCount);
        int bytesWritten = rune.EncodeToUtf8(destination);
        sendBufferCount += bytesWritten;
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
    
    private void DrawMouseCursor(TmGuiBackend backend)
    {
        var mouse   = backend.input.MousePos;
        var x       = (int)(mouse.X / tuiBatch.CharWidth)  - 1;
        var y       = (int)(mouse.Y / tuiBatch.LineHeight) - 1;
        
        var cell = new TuiColorCell {
            textStyle  = TextStyle.None, 
            color      = 0xffffffff,
            background = 0x606060ff
        };
        var buffer  = frameBuffer;
        var shape   = MouseCursorShape.Cursors[(int)backend.input.CurrentCursor];
        
        buffer.SetCell(x - 1, y, cell with { rune = new Rune(shape.left)   });
        buffer.SetCell(x,     y, cell with { rune = new Rune(shape.center) });
        buffer.SetCell(x + 1, y, cell with { rune = new Rune(shape.right)  });
    }
    
    private bool AppendSixel(byte sixelId, int x, int y)
    {
        ref var drawSixel = ref tuiBatch.drawSixels[sixelId];
        if (drawSixel.isDrawn) {
            SetCursorPos(y + 1, x + 2);
            return true;
        }
        drawSixel.isDrawn = true;
        var sixel   = drawSixel.sixel;
        var target  = sendBuffer.AsSpan(sendBufferCount, sendBuffer.Length - sendBufferCount);
        var bytesWritten = sixelDrawer.AppendSixelToTargetBuffer(drawSixel, tuiBatch, target, cellPixelSize, x, y);
        sendBufferCount += bytesWritten;
        
        SetCursorPos(y + 1, x + 2);
        return true;
    }
}
