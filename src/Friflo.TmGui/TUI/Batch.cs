// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

// ReSharper disable UseWithExpressionToCopyStruct
// ReSharper disable InlineTemporaryVariable
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.TUI;

public sealed class TuiBatch : TmBatch
{
    public              TuiBorder               buttonBorder    = new('[', ']');
    public              TuiBorder               focusBorder     = new('[', ']');
    private             float                   yScale;
    private             float                   xScale;
    private             float                   lineHeight;
    private             float                   charWidth;
    private             int                     rectStart;
    internal readonly   List<TuiRect>           tuiRects        = [];
    private  readonly   List<TuiRectCommand>    rectCommands    = [];
    private  readonly   List<char>              textBuffer      = [];
    private  readonly   TuiBackend              backend;
    
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
    
#region internal
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
        rectCommands.Add(new TuiRectCommand(currentZIndex.value, currentSequence++, view, currentScissor.pos, currentScissor.pos + currentScissor.size));
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
#endregion

#region DrawRectCommands
    private void DrawRectCommands(int stride, bool drawColor, Span<TuiColorCell> cells, Span<char> chars)
    {
        var commands    = rectCommands;
        var rects       = tuiRects;
        var texts       = CollectionsMarshal.AsSpan(textBuffer);
        
        foreach (var segment in commandSegments)
        {
            var lastCmd = segment.index + segment.length;
            for (int cmdIndex = segment.index; cmdIndex < lastCmd; cmdIndex++)
            {
                var cmd       = commands[cmdIndex];
                var scissorL  = (int)(cmd.scissorTL.X * xScale);
                var scissorT  = (int)(cmd.scissorTL.Y * yScale);
                var scissorR  = (int)(cmd.scissorBR.X * xScale);
                var scissorB  = (int)(cmd.scissorBR.Y * yScale);
                
                var lastRect    = cmd.rectView.offset + cmd.rectView.length;
                for (int index  = cmd.rectView.offset; index < lastRect; index++)
                {
                    var rect    = rects[index];
                    var rectL   = (int)(rect.TL.X * xScale);
                    var rectT   = (int)(rect.TL.Y * yScale);
                    var rectR   = (int)(rect.BR.X * xScale);
                    var rectB   = (int)(rect.BR.Y * yScale);

                    // Fast AABB intersection clipping against scissor bounds
                    int startX = Math.Max(rectL, scissorL);
                    int startY = Math.Max(rectT, scissorT);
                    int endX   = Math.Min(rectR, scissorR);
                    int endY   = Math.Min(rectB, scissorB);

                    // Early exit for fully clipped rectangles
                    if (startX >= endX || startY >= endY) continue;

                    // Text rendering branch with two-sided horizontal clipping
                    if (rect.text.len != 0)
                    {
                        var text = texts.Slice(rect.text.start, rect.text.len);

                        // Offset for left-side clipping
                        int offsetX = startX - rectL;

                        // Clamp character count strictly against right scissor bound (endX)
                        int maxVisibleWidth = endX - startX;
                        int availableText   = text.Length - offsetX;
                        int count           = Math.Min(availableText, maxVisibleWidth);

                        if (count > 0 && startY == rectT)
                        {
                            if (drawColor) {
                                var cell    = new TuiColorCell { color = rect.color, background = rect.background };
                                var row     = cells.Slice(stride * startY + startX, count);
                                for (int n = 0; n < count; n++) {
                                    cell.character = text[offsetX + n];
                                    row[n] = cell;
                                }
                            } else {
                                var srcSpan = text.Slice(offsetX, count);
                                var dstSpan = chars.Slice(stride * startY + startX, count);
                                srcSpan.CopyTo(dstSpan);
                            }
                        }
                        continue;
                    } 
                    // Fill clipped background area row by row
                    if (drawColor) {
                        var width = endX - startX;
                        var fill  = new TuiColorCell { character = ' ', color = rect.color, background = rect.background };

                        for (int y = startY; y < endY; y++) {
                            cells.Slice(stride * y + startX, width).Fill(fill);
                        }
                    } else {
                        var width = endX - startX;
                        for (int y = startY; y < endY; y++) {
                            chars.Slice(stride * y + startX, width).Fill(' ');
                        }
                    }
                }
            }
        }
    }

    /// <summary> Result in <see cref="TuiBackend.ColorCells"/> </summary>
    public void DrawRectCommandsColor(int targetWidth, int targetHeight, TuiColorCell clear)
    {
        EndTuiBatch();
        backend.PrepareColorCells(targetWidth, targetHeight);
        
        var cells       = backend.ColorCells;
        cells.Fill(clear);
        
        DrawRectCommands(targetWidth, true, cells, default);
    }
    
    
    /// <summary> Result in <see cref="TuiBackend.CharCells"/> </summary>
    public void DrawRectCommandsChar(int targetWidth, int targetHeight, char clear, ReadOnlySpan<char> lineEnd)
    {
        EndTuiBatch();
        
        int stride = targetWidth + lineEnd.Length;
        backend.PrepareCharCells(stride, targetHeight);
        
        var chars = backend.CharCells;
        chars.Fill(clear);
        
        DrawRectCommands(stride, false, default, chars);
        
        if (lineEnd.Length == 0) {
            return;
        }
        for (int line = 0; line < targetHeight; line++) {
            lineEnd.CopyTo(chars.Slice(line * stride + targetWidth, lineEnd.Length));
        }
    }
#endregion


#region Draw / Widget methods
    public Vector2 DrawText(ReadOnlySpan<char> text, Vector2 position, Color32 color, Color32 background)
    {
        var textSpan    = new TextSpan { start = textBuffer.Count, len = text.Length };
        tuiRects.Add(new TuiRect(textSpan, position, new Vector2(textSpan.len * charWidth, lineHeight), color, background));
        textBuffer.AddRange(text);
        return new Vector2(lineHeight * text.Length, lineHeight);
    }
    
    public void FillRect(Vector2 position, Vector2 size, Color32 background)
    {
        tuiRects.Add(new TuiRect(position, size, background));
    }
    
    public void Button(ReadOnlySpan<char> text, Vector2 position, Vector2 size, Color32 color, Color32 background)
    {
        var textStart = textBuffer.Count;
        textBuffer.Add(buttonBorder.left);
        textBuffer.AddRange(text);
        textBuffer.Add(buttonBorder.right);
        var textSpan    = new TextSpan { start = textStart, len = textBuffer.Count - textStart };
        tuiRects.Add(new TuiRect(textSpan, position, size, color, background));
    }
    
    public void Checkbox(bool value, ReadOnlySpan<char> text, Vector2 position, Vector2 size, Color32 color, Color32 boxColor, Color32 background)
    {
        var textStart   = textBuffer.Count;
        var boxText     = value ? "[x]" : "[ ]";
        textBuffer.AddRange(boxText.AsSpan());
        var boxSpan     = new TextSpan { start = textStart, len = 3 };
        tuiRects.Add(new TuiRect(boxSpan, position, new Vector2(3 * charWidth, lineHeight), color, boxColor));
        
        textBuffer.AddRange(text);
        var textSpan    = new TextSpan { start = textStart + 3, len = textBuffer.Count - textStart - 3 };
        tuiRects.Add(new TuiRect(textSpan, position + new Vector2(4 * charWidth, 0), size, color, background));
    }

    public void Slider(ReadOnlySpan<char> name, ref float value, float min, float max, float width, Vector2 position, Vector2 size)
    {

    }

    public void DrawScrollbar(Vector2 position, Vector2 size, Color32 background)
    {
        tuiRects.Add(new TuiRect(position, size, background));
    }
    
    internal void DrawFocus(Vector2 pos, Vector2 size, Color32 color, Color32 background)
    {
        var textStart   = textBuffer.Count;
        textBuffer.Add(focusBorder.left);
        textBuffer.Add(focusBorder.right);
        var charSize    = new Vector2(charWidth, lineHeight);
        var markLeft    = new TextSpan { start = textStart,     len = 1 };
        var markRight   = new TextSpan { start = textStart + 1, len = 1 };
        
        tuiRects.Add(new TuiRect(markLeft,  pos,  charSize, color, background));
        tuiRects.Add(new TuiRect(markRight, pos + new Vector2(size.X - charWidth, 0),     charSize, color, background));
    }
#endregion
}