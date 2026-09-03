// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.TUI;

public sealed class TuiBatch : TmBatch
{
    private             float                   yScale;
    private             float                   xScale;
    private             float                   lineHeight;
    private             float                   charWidth;
    private             int                     rectStart;
    private readonly    List<TuiRect>           tuiRects        = [];
    private readonly    List<TuiRectCommand>    rectCommands    = [];
    private readonly    List<char>              textBuffer      = [];
    private readonly    TuiBackend              backend;
    
    public              float                   CharWidth   => charWidth;
    public              float                   LineHeight  => lineHeight;

    public TuiBatch(TuiBackend backend) : base(backend, 0) {
        this.backend = backend;
    }

    private const float CharacterAspectRatio = 0.5f;

    protected internal override void InitBatch()
    {
        lineHeight  = backendDefaultFont.lineHeight;
        charWidth   = lineHeight * CharacterAspectRatio;
        yScale      = 1f / lineHeight;
        xScale      = yScale / CharacterAspectRatio;
    }
    
    internal void Reset()
    {
        rectStart   = 0;   
        tuiRects.Clear();
        rectCommands.Clear();
        textBuffer.Clear();
    }
    
    internal void FlushRects()
    {
        int rectCount = tuiRects.Count - rectStart;
        if (rectCount <= 0) {
            return;
        }
        var view    = new RectView(rectStart, rectCount);
        rectStart   = tuiRects.Count;
        var tl      = new TuiVector(xScale * currentScissor.pos.X,         yScale * currentScissor.pos.Y);
        var br      = new TuiVector(xScale * currentScissor.size.X + tl.x, yScale * currentScissor.size.Y + tl.y);
        rectCommands.Add(new TuiRectCommand(currentZIndex.value, currentSequence++, view, tl, br));
    }
    

    
    private static void SortRectCommands(List<TuiRectCommand> commands, List<CmdSegment> segments)
    {
        // commands.Sort((a, b) => (a.zIndex, a.sequence).CompareTo((b.zIndex, b.sequence)));
        
        // Run-Length optimization - of commented Sort() above
        var command_0   = commands[0];
        var zIndex      = command_0.zIndex;
        var segment     = new CmdSegment { zIndex = zIndex, sequence = command_0.sequence, index = 0, length = 1 };
        
        for (int n = 1; n < commands.Count; n++)
        {
            var cmd = commands[n];
            if (zIndex == cmd.zIndex) {
                segment.length++;
                continue;
            }
            segments.Add(segment);
            zIndex              = cmd.zIndex;
            segment.zIndex      = zIndex;
            segment.sequence    = cmd.sequence;
            segment.index       = n;
            segment.length      = 1;
        }
        segments.Add(segment);
        
        segments.Sort((a, b) => (a.zIndex, a.sequence).CompareTo((b.zIndex, b.sequence)));
    }
    
    private void EndTuiBatch()
    {
        FlushRects();
        
        var segments = commandSegments;
        segments.Clear();

        if (sortZIndex) {
            SortRectCommands(rectCommands, segments);
        } else {
            segments.Add(new CmdSegment { index = 0, length = rectCommands.Count });
        }
    }
    

    public void DrawRectCommands(int targetWidth, int targetHeight)
    {
        EndTuiBatch();
        
        var commands    = rectCommands;
        var rects       = tuiRects;
        var texts       = CollectionsMarshal.AsSpan(textBuffer);
        backend.PrepareBuffers(targetWidth, targetHeight);
        
        var cells         = backend.Cells;
        var clear         = new TuiCell { character = '.' };
        cells.Fill(clear);
        
        foreach (var segment in commandSegments)
        {
            var lastCmd = segment.index + segment.length;
            for (int cmdIndex = segment.index; cmdIndex < lastCmd; cmdIndex++)
            {
                var cmd       = commands[cmdIndex];
                var scissorTL = cmd.scissorTL;
                var scissorBR = cmd.scissorBR;
                
                var lastRect    = cmd.rectView.offset + cmd.rectView.length;
                for (int index  = cmd.rectView.offset; index < lastRect; index++)
                {
                    var rect = rects[index];

                    // Fast AABB intersection clipping against scissor bounds
                    int startX = Math.Max(rect.TL.x, scissorTL.x);
                    int startY = Math.Max(rect.TL.y, scissorTL.y);
                    int endX   = Math.Min(rect.BR.x, scissorBR.x);
                    int endY   = Math.Min(rect.BR.y, scissorBR.y);

                    // Early exit for fully clipped rectangles
                    if (startX >= endX || startY >= endY) continue;

                    // Text rendering branch with two-sided horizontal clipping
                    if (rect.text.len != 0)
                    {
                        var cell = new TuiCell { color = rect.color, background = rect.background };
                        var text = texts.Slice(rect.text.start, rect.text.len);

                        // Offset for left-side clipping
                        int offsetX = startX - rect.TL.x;

                        // Clamp character count strictly against right scissor bound (endX)
                        int maxVisibleWidth = endX - startX;
                        int availableText   = text.Length - offsetX;
                        int count           = Math.Min(availableText, maxVisibleWidth);

                        if (count > 0 && startY == rect.TL.y)
                        {
                            var row = cells.Slice(targetWidth * startY + startX, count);
                            for (int n = 0; n < count; n++) {
                                cell.character = text[offsetX + n];
                                row[n] = cell;
                            }
                        }
                        continue;
                    } 

                    // Fill clipped background area row by row via SIMD Span.Fill
                    var width = endX - startX;
                    var fill  = new TuiCell { character = ' ', color = rect.color, background = rect.background };

                    for (int y = startY; y < endY; y++) {
                        cells.Slice(targetWidth * y + startX, width).Fill(fill);
                    }
                }
            }
        }
        // fill StridedFrameBuffer
        var buffer = backend.StridedFrameBuffer;
        int pos = 0;
        for (int line = 0; line < targetHeight; line++) {
            for (int col = 0; col < targetWidth; col++) {
                buffer[pos++] = cells[line * targetWidth + col].character;
            }
            buffer[pos++] = '\n';
        }
    }

    public Vector2 DrawText(ReadOnlySpan<char> text, Vector2 position, Color32 color, Color32 background)
    {
        var textSpan    = new TextSpan { start = textBuffer.Count, len = text.Length };
        var tl          = new TuiVector(position.X * xScale, position.Y * yScale);
        var br          = new TuiVector(tl.x + textSpan.len, tl.y + 1);
        tuiRects.Add(new TuiRect(textSpan, tl, br, color, background));
        textBuffer.AddRange(text);
        return new Vector2(lineHeight * text.Length, lineHeight);
    }
    
    public void FillRect(Vector2 position, Vector2 size, Color32 background)
    {
        var tl          = new TuiVector(position.X * xScale, position.Y * yScale);
        var tuiSize     = new TuiVector(size.X     * xScale, size.Y     * yScale);
        tuiRects.Add(new TuiRect(tl, tuiSize, background));
    }
    
    public void Button(ReadOnlySpan<char> text, Vector2 position, Vector2 size, Color32 color, Color32 background)
    {
        var textStart = textBuffer.Count;
        textBuffer.Add('[');
        textBuffer.AddRange(text);
        textBuffer.Add(']');
        var textSpan    = new TextSpan { start = textStart, len = textBuffer.Count - textStart };
        var lf          = new TuiVector(position.X * xScale,        position.Y * yScale);
        var br          = new TuiVector(size.X     * xScale + lf.x, size.Y     * yScale + lf.y);
        tuiRects.Add(new TuiRect(textSpan, lf, br, color, background));
    }
    
    public void Checkbox(bool value, ReadOnlySpan<char> text, Vector2 position, Vector2 size, Color32 color, Color32 background)
    {
        var textStart = textBuffer.Count;
        textBuffer.AddRange(value ? "[x] " : "[ ] ");        
        textBuffer.AddRange(text);
        var textSpan    = new TextSpan { start = textStart, len = textBuffer.Count - textStart };
        var lf          = new TuiVector(position.X * xScale,        position.Y * yScale);
        var br          = new TuiVector(size.X     * xScale + lf.x, size.Y     * yScale + lf.y);
        tuiRects.Add(new TuiRect(textSpan, lf, br, color, background));
    }

    public void Slider(ReadOnlySpan<char> name, ref float value, float min, float max, float width, Vector2 position, Vector2 size)
    {

    }

    public void DrawScrollbar(Vector2 position, Vector2 size, Color32 background)
    {
        var tuiPos      = new TuiVector(position.X * xScale, position.Y * yScale);
        var tuiSize     = new TuiVector(size.X     * xScale, size.Y     * yScale);
        tuiRects.Add(new TuiRect(tuiPos, tuiSize, background));
    }
}