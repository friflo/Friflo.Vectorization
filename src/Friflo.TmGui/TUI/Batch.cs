// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UseWithExpressionToCopyStruct
// ReSharper disable InlineTemporaryVariable
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.TUI;

public sealed partial class TuiBatch : TmBatch
{
    public              TuiFocusBorder          focusBorder;
    private             float                   yScale;
    private             float                   xScale;
    private             float                   lineHeight;
    private             float                   charWidth;
    private             int                     rectStart;
    internal readonly   List<TuiRect>           tuiRects        = [];
    private  readonly   List<TuiRectCommand>    rectCommands    = [];
    private  readonly   List<char>              textBuffer      = [];
    private  readonly   List<Color32>           colorBuffer     = [];
    
    public              ReadOnlySpan<char>      Texts       => CollectionsMarshal.AsSpan(textBuffer);
    public              ReadOnlySpan<Color32>   Colors      => CollectionsMarshal.AsSpan(colorBuffer);
    public              ReadOnlySpan<TuiRect>   Rects       => CollectionsMarshal.AsSpan(tuiRects);
    public              float                   CharWidth   => charWidth;
    public              float                   LineHeight  => lineHeight;
    public              float                   XScale      => xScale;
    public              float                   YScale      => yScale;

    public TuiBatch(TuiBackend backend, TuiColorMode colorMode) : base(backend, 0)
    {
        if  (colorMode == TuiColorMode.Monochrome) {
            focusBorder  = new TuiFocusBorder('>', '<');
        } else {
            focusBorder  = new TuiFocusBorder('[', ']');
        }
    }

    private const float CharacterAspectRatio = 0.5f;

    protected internal override void InitBatch()
    {
        lineHeight  = backendDefaultFont.lineHeight;
        charWidth   = lineHeight * CharacterAspectRatio;
        yScale      = 1f / lineHeight;
        xScale      = yScale / CharacterAspectRatio;
    }
    
    // Snap position to center of terminal grid cells for TUI mode
    internal void SnapPositionToGrid(ref Vector2 position)
    {
        position.X = (0.5f + MathF.Floor(position.X * xScale)) * charWidth;
        position.Y = (0.5f + MathF.Floor(position.Y * yScale)) * lineHeight;
    }
    
    // Snap extent (sizes, bounds, scroll offsets, or relative deltas) to nearest terminal grid cell for TUI mode
    internal void SnapExtentToGrid(ref Vector2 vector)
    {
        vector.X = MathF.Floor((vector.X + charWidth  * 0.5f) * xScale) * charWidth;
        vector.Y = MathF.Floor((vector.Y + lineHeight * 0.5f) * yScale) * lineHeight;
    }
    
#region internal
    internal void Reset()
    {
        rectStart   = 0;   
        tuiRects.Clear();
        rectCommands.Clear();
        textBuffer.Clear();
        colorBuffer.Clear();
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
        var colors      = CollectionsMarshal.AsSpan(colorBuffer);
        
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
                                var color     = rect.color;
                                var textStyle = rect.textStyle;
                                var row       = cells.Slice(stride * startY + startX, count);

                                // Calculate span boundary; evaluates to 0 for solid colors or full left-clipping
                                int remaining   = color.len - offsetX;
                                int spanEnd     = remaining <= 0 ? 0 : remaining < count ? remaining : count;

                                // Phase 1: Direct 1:1 color mapping for available span entries
                                for (int n = 0; n < spanEnd; n++) {
                                    ref var dstCell = ref row[n];
                                    dstCell.character   = text[offsetX + n];
                                    dstCell.color       = colors[color.start + offsetX + n];
                                    dstCell.textStyle   = textStyle;
                                }

                                // Phase 2: Tail fill for remaining characters using solid color or the last span color
                                if (spanEnd < count) {
                                    var solidColor = color.len == 0 ? color.value : colors[color.start + color.len - 1];
                                    for (int n = spanEnd; n < count; n++) {
                                        ref var dstCell     = ref row[n];
                                        dstCell.character   = text[offsetX + n];
                                        dstCell.color       = solidColor;
                                        dstCell.textStyle   = textStyle;
                                    }
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
                        var width   = endX - startX;
                        var fill    = new TuiColorCell { character = rect.text.fillChar, color = 0, background = rect.color.value };
                        if (rect.color.len == 2) {
                            fill.background = colors[rect.color.start];
                            fill.color      = colors[rect.color.start + 1];
                        }
                        for (int y = startY; y < endY; y++) {
                            cells.Slice(stride * y + startX, width).Fill(fill);
                        }
                    } else {
                        var width = endX - startX;
                        for (int y = startY; y < endY; y++) {
                            chars.Slice(stride * y + startX, width).Fill(rect.text.fillChar);
                        }
                    }
                }
            }
        }
    }

    /// <summary> Result in <see cref="FrameBuffer.ColorCells"/> </summary>
    public void DrawRectCommandsColor(FrameBuffer frameBuffer, int targetWidth, int targetHeight, TuiColorCell clear)
    {
        EndTuiBatch();
        frameBuffer.PrepareColorCells(targetWidth, targetHeight);
        
        var cells = frameBuffer.ColorCells;
        cells.Fill(clear);
        
        DrawRectCommands(targetWidth, true, cells, default);
    }
    
    
    /// <summary> Result in <see cref="FrameBuffer.CharCells"/> </summary>
    /// <remarks>
    /// lineEnd ("\r\n") is added to each line. Is used when writing a screen to a text file are a terminal. 
    /// </remarks>
    public void DrawRectCommandsChar(FrameBuffer frameBuffer, int targetWidth, int targetHeight, char clear, ReadOnlySpan<char> lineEnd)
    {
        EndTuiBatch();
        
        int stride = targetWidth + lineEnd.Length;
        frameBuffer.PrepareCharCells(stride, targetHeight);
        
        var chars = frameBuffer.CharCells;
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
}