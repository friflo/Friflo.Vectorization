// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Friflo.TmGui.Session;

// ReSharper disable InconsistentNaming
// ReSharper disable InlineTemporaryVariable
// ReSharper disable CanSimplifyStringEscapeSequence
// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.TUI.VT100;


internal sealed class TuiSessionShared
{
    internal readonly   FrameBuffer     frameBuffer = new();
    internal readonly   SixelDrawer     sixelDrawer = new();
}


internal sealed partial class TuiSession : TmSession
{
    private  readonly   TmClient            client;         // instance: passed
    private  readonly   TuiColorMode        colorMode;
    private  readonly   TuiSessionShared    shared;         // instance: shared
    private  readonly   TuiBackend          tuiBackend;     // instance: creates / owns
    internal readonly   TuiBatch            tuiBatch;       // instance: creates / owns
    private  readonly   byte[]              sendBuffer      = new byte[60000];  // TODO  should be shared / grow if needed
    private             int                 sendBufferCount;
    private             int                 frameWidth      = 50;
    private             int                 frameHeight     = 20;
    private             bool                supportsSixel;
    private             Vector2             cellPixelSize   = new(10, 20);
    private             bool                sessionStart;
    //
    private             ulong               lastFrameHash;
    private             ulong[]             lastLineHashes  = new ulong[10];
    private             int                 sendCounter;
    
    internal TuiSession(TmClient client, SessionId sessionId, TuiSessionShared shared, FrameTimer frameTimer, IGuiAssets assets, TuiColorMode colorMode)
        : base(sessionId)
    {
        this.client         = client;
        this.colorMode      = colorMode;
        this.shared         = shared;
        tuiBackend          = new TuiBackend("Terminal", assets);
        
        tuiBatch            = tuiBackend.CreateBatch(colorMode);
        tuiBatch.session    = this;
        tuiBatch.frameTimer = frameTimer;
    }
    
    // --- TmSession
    protected internal override TmGuiBackend    Backend => tuiBackend;
    
    internal override GuiReplay CreateReplay()
    {
        var replayBackend   = new TuiBackend("Replay", tuiBackend.Assets);
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
        lastFrameHash   = 0; // force send frame
        Array.Clear(lastLineHashes);
    }
    
    private void SetCellPixelSize(int width, int height)
    {
        cellPixelSize = new Vector2(width, height);
        tuiBatch.terminalPixelSize = new Vector2(tuiBatch.CharWidth / width, tuiBatch.LineHeight / height);
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
        // \x1b[c       = Request primary Device Attributes Query (DA1) - to detect Sixel support
        // \x1b[?1l     = Normal Cursor Mode
        // \x1b[?25l    = Hide Cursor
        // \x1b[0m      = Reset All Colors/Attributes
        // \x1b[?1049h  = Switch to Alternate Screen-Buffer - Only reliable way to avoid flickering when resize to smaller screen
        // \x1b[2J      = Clear Screen
        // \x1b[3J      = Clear Scrollback-Buffer           - prevents Alternate Screen-Buffer Reflow-Ghosting
        // \x1b[?7l     = Disable Auto-Wrap
        // \x1b[H       = Home Cursor (0,0)
        AppendSpan("\x1b[14t\x1b[16t\x1b[c\x1b[?1l\x1b[?25l\x1b[0m\x1b[?1049h\x1b[2J\x1b[3J\x1b[?7l\x1b[H"u8);
        
        AppendSpan("\x1b[?1003h"u8);    // Enable mouse hover (tracks ALL movement, clicks & scrolling)
        AppendSpan("\x1b[?1006h"u8);    // Enable SGR extended coordinate format (required for modern terminals & high resolutions)
        AppendSpan("\x1b[18t"u8);       // Request current terminal size from terminal via stdout - answer handled by HandleInBandResize()
    }
    
    internal override Memory<byte> IterateUI(in AssetResources resources)
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
        var frameHash   = HashUtils.XxHash3(sendMemory.Span);
        if (frameHash == lastFrameHash) {
            return default;
        }
        sendCounter++;
        // Debug.WriteLine(sendBufferCount);
        // Debug.Write(sendCounter); Debug.WriteLine(" - send buffer");
        lastFrameHash = frameHash;
        return sendMemory;
    }
    
    private void AppendFrameBuffer(TmGuiBackend backend, TuiBatch batch, int width, int height)
    {
        var clear =  new TuiColorCell { Character = ' ', color = 0x000000ff, background = 0x888888ff };
        batch.DrawRectCommands(shared.frameBuffer, width, height, clear);
        
        if (backend.input.CurrentCursor != MouseCursor.Arrow) {
            DrawMouseCursor(backend);
        }
        
        var drawSixels = tuiBatch.drawSixels.AsSpan(0, tuiBatch.drawSixelCount + 1);
        
        if (height > lastLineHashes.Length) {
            var newHashes = new ulong[Math.Max(height, 2 * lastLineHashes.Length)];
            Array.Copy(lastLineHashes, 0, newHashes, 0, lastLineHashes.Length);
            lastLineHashes = newHashes;
        }
        
        if (!supportsSixel) {
            drawSixels = default;
        }

        // draw all cells not covered by a sixel
        AppendCellRect(0, 0, width, height, width, drawSixels, lastLineHashes);
        
        // draw all sixels and the cells covered by those sixels
        if (supportsSixel) {
            AppendSixels(width, height);
        }
    }
    
    private void AppendCellRect(int left, int top, int right, int bottom, int width, Span<DrawSixel> drawSixels, Span<ulong> lineHashes)
    {
        var drawAlways = drawSixels.IsEmpty;
            
        // color / background are only sent if changed
        var stateChanged    = false;
        var color           = new Color32();
        var background      = new Color32();
        var textStyle       = TextStyle.None;
        AppendSpan("\x1b[0;30;40m"u8); // \x1b[0 Reset All;  30 Foreground Black;  40 Background Black  m SGR Command Terminator
        var cells = shared.frameBuffer.ColorCells;

        for (int y = top; y < bottom; y++)
        {
            var lineStart     = sendBufferCount;

            AppendCursor(left + 1, y + 1);
            
            for (int x = left; x < right; x++)
            {
                var cell = cells[y * width + x];

                if (cell.color.A != 0) {
                    var colorRGB = cell.color.Packed & 0x00ffffff;
                    if (colorRGB != color.Packed) {
                        AppendColor(cell.color);
                        color.Packed = colorRGB;
                    }
                }
                if (cell.textStyle != textStyle) {
                    ApplyStyleDiff(textStyle, cell.textStyle);
                    textStyle = cell.textStyle;
                }
                var backgroundRGB = cell.background.Packed & 0x00ffffff;
                if (backgroundRGB != background.Packed) {
                    AppendBackground(cell.background);
                    background.Packed = backgroundRGB;
                }
                
                if (cell.sixelId == 0 || drawAlways) { 
                     AppendRune(cell.rune);
                } else {
                    drawSixels[cell.sixelId].Draw(x, y, backgroundRGB ^ (uint)cell.rune.Value ^ cell.color.Packed);

                    AppendSpan("\x1b[C"u8); // move cursor one cell right
                }
            }
            // var lineSpan = DedupRLE(lineStart);
            var lineSpan = sendBuffer.AsSpan(lineStart, sendBufferCount - lineStart);
            
            if (lineHashes.IsEmpty) {
                continue;
            }
            
            // --- send only changed lines

            var lineHash    = HashUtils.XxHash3(lineSpan);
            var sendLine    = lineHash != lineHashes[y];
            if (sendLine) {
                lineHashes[y]   = lineHash;
                stateChanged    = true;
                continue;
            }
            // case:  line is unchanged - skip sending
            sendBufferCount = lineStart;
            // reset state for next line. Next line cannot relay on a specific state
            if (stateChanged) {
                AppendSpan("\x1b[0;30;40m"u8); // \x1b[0 Reset All;  30 Foreground Black;  40 Background Black  m SGR Command Terminator
            }
            stateChanged    = false;
            color           = new Color32();
            background      = new Color32();
            textStyle       = TextStyle.None;
        }
    }
    
    private readonly List<uint> sixelHashes = [];
    
    private void AppendSixels(int width, int height)
    {
        var cells = shared.frameBuffer.ColorCells;

        var sixelDrawer = shared.sixelDrawer;
        sixelDrawer.SetClipCells(cells, width, height);
        var batch = tuiBatch;
        
        for (int n = 1; n <= batch.drawSixelCount; n++)
        {
            ref var drawSixel = ref batch.drawSixels[n];
            if (!drawSixel.draw) {
                continue;
            }
            drawSixel.sixelHash = drawSixel.UpdateHash();
            
            if (sixelHashes.Contains(drawSixel.sixelHash)) {
                continue;
            }
            AppendCellRect(drawSixel.left, drawSixel.top, drawSixel.right + 1, drawSixel.bottom + 1, width, default, default);
            
            var target  = sendBuffer.AsSpan(sendBufferCount, sendBuffer.Length - sendBufferCount);
            var bytesWritten = sixelDrawer.AppendSixelToTargetBuffer(drawSixel, batch, target, cellPixelSize);
            sendBufferCount += bytesWritten;
            // Debug.WriteLine($"Draw Sixel: {drawSixel.sixelId}");
        }
        
        sixelHashes.Clear();
        for (int n = 1; n <= batch.drawSixelCount; n++) {
            sixelHashes.Add(batch.drawSixels[n].sixelHash);
        }
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
        var buffer  = shared.frameBuffer;
        var shape   = MouseCursorShape.Cursors[(int)backend.input.CurrentCursor];
        
        buffer.SetCell(x - 1, y, cell with { rune = new Rune(shape.left)   });
        buffer.SetCell(x,     y, cell with { rune = new Rune(shape.center) });
        buffer.SetCell(x + 1, y, cell with { rune = new Rune(shape.right)  });
    }
}
