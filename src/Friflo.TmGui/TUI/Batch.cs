// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

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
    private readonly    string                  batchName;
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

    public   override   string                  ToString()  => batchName;

    public TuiBatch(TuiBackend backend, TuiColorMode colorMode) : base(backend, 0)
    {
        batchName = backend.name;
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
    private static readonly Rune Ellipsis = new('…');
    
    /// Fast alternative for <see cref="MathF.Floor"/> especially in DEBUG
    private static int FastFloor(float x) {
        int i = (int)x;
        return i > x ? i - 1 : i;
    }
    
    private void DrawRectCommandsInternal(int stride, Span<TuiColorCell> cells)
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
                    // Note! MathF.Floor() is slow especially in DEBUG
                    var rectL   = FastFloor(rect.TL.X * xScale);
                    var rectT   = FastFloor(rect.TL.Y * yScale);
                    var rectR   = FastFloor(rect.BR.X * xScale);
                    var rectB   = FastFloor(rect.BR.Y * yScale);

                    // Fast AABB intersection clipping against scissor bounds
                    int startX  = Math.Max(rectL, scissorL);
                    int startY  = Math.Max(rectT, scissorT);
                    int endX    = Math.Min(rectR, scissorR);
                    int endY    = Math.Min(rectB, scissorB);

                    // Early exit for fully clipped rectangles
                    if (startX >= endX || startY >= endY) continue;
                    
                    if (rect.sixelHandle != 0) {
                        cells[startY * stride + startX].sixelHandle = rect.sixelHandle;
                        continue;
                    }

                    if (rect.text.len == 0)
                    {
                        // -----------------------------------------------------------
                        // case: Fill clipped background area row by row with split-wide cell repairs
                        int width = endX - startX;
                        var fill  = new TuiColorCell { 
                            rune       = new Rune(rect.text.fillChar), 
                            width      = 1, 
                            color      = 0, 
                            background = rect.color.value 
                        };
                        if (rect.color.len == 2) {
                            fill.background = colors[rect.color.start];
                            fill.color      = colors[rect.color.start + 1];
                        }
                        if (fill.rune.Value == 0) {
                            for (int y = startY; y < endY; y++) {
                                for (int x = startX; x < endX; x++) {
                                    ref var cell = ref cells[y * stride + x];
                                    cell.background = Color32.BlendFast(cell.background, fill.background);
                                    cell.color      = Color32.BlendFast(cell.color,      fill.color);
                                }
                            }
                            continue;
                        }
                        for (int y = startY; y < endY; y++) {
                            var fillRow = cells.Slice(stride * y, stride);

                            // Fix orphan wide rune on the left edge
                            if (startX > 0 && fillRow[startX].width == 0) {
                                ref var left    = ref fillRow[startX - 1];
                                left.rune       = Ellipsis;
                                left.width      = 1;
                            }
                            // Fix orphan ghost cell on the right edge
                            if (endX < stride && fillRow[endX - 1].width == 2) {
                                ref var right   = ref fillRow[endX];
                                right.rune      = Ellipsis;
                                right.width     = 1;
                            }
                            fillRow.Slice(startX, width).Fill(fill);
                        }
                        continue;
                    }
                    // ---------------------------------------------------------------
                    // case: Text rendering branch with two-sided horizontal clipping
                    int maxVisibleWidth = endX - startX;

                    // Guard clause: Skip rendering if out of vertical bounds or horizontally collapsed
                    if (maxVisibleWidth <= 0 || startY != rectT) {
                        continue;
                    }

                    Span<char> text = texts.Slice(rect.text.start, rect.text.len);

                    // Offset for left-side clipping
                    int offsetX     = startX - rectL;

                    // Fast-forward textPos past left-clipped characters
                    int textPos     = 0;
                    int skippedCols = 0;

                    while (skippedCols < offsetX && textPos < text.Length) {
                        Rune.DecodeFromUtf16(text.Slice(textPos), out var r, out int consumed);
                        int w = r.RuneWidth;
                        if (skippedCols + w > offsetX) break; // Straddle hit! Stop fast-forwarding

                        skippedCols += w;
                        textPos += consumed;
                    }
                    var row        = cells.Slice(stride * startY + startX, maxVisibleWidth);
                    var color      = rect.color;
                    var textStyle  = rect.textStyle;
                    // Pre-calculate fallback color for tail entries
                    var solidColor = color.len == 0 ? color.value : colors[color.start + color.len - 1];

                    int n = 0;

                    // Handle left-edge straddle before main loop: Wide char cut in half on left border
                    if (skippedCols < offsetX && textPos < text.Length) {
                        Rune.DecodeFromUtf16(text.Slice(textPos), out _, out int consumed);
                        textPos += consumed;

                        ref var cell   = ref row[0];
                        cell.rune      = Ellipsis;
                        cell.color     = offsetX < color.len ? colors[color.start + offsetX] : solidColor;
                        cell.textStyle = textStyle;
                        cell.width     = 1;

                        n = 1;
                    }

                    // Single pass rendering loop (Handles visible text & right clipping)
                    while (n < row.Length && textPos < text.Length) {
                        ref var dstCell = ref row[n];
                        Rune.DecodeFromUtf16(text.Slice(textPos), out dstCell.rune, out int charsConsumed);
                        textPos += charsConsumed;

                        int col           = offsetX + n;
                        dstCell.color     = col < color.len ? colors[color.start + col] : solidColor;
                        dstCell.textStyle = textStyle;

                        int runeWidth = dstCell.rune.RuneWidth;

                        if (runeWidth == 2) {
                            if (n + 1 < row.Length) {
                                dstCell.width = 2;

                                ref var ghost   = ref row[n + 1];
                                ghost.rune      = default;
                                ghost.color     = dstCell.color;
                                ghost.textStyle = textStyle;
                                ghost.width     = 0;
                                n += 2;
                            } else {
                                // Right border clip: Replace partially visible wide character with Ellipsis
                                dstCell.rune  = Ellipsis;
                                dstCell.width = 1;
                                n += 1;
                            }
                        } else {
                            dstCell.width = (byte)runeWidth;
                            n += 1;
                        }
                    }
                    // end: Text rendering branch
                }
            }
        }
    }

    /// <summary> Result in <see cref="FrameBuffer.ColorCells"/> </summary>
    public void DrawRectCommands(FrameBuffer frameBuffer, int targetWidth, int targetHeight, TuiColorCell clear)
    {
        EndTuiBatch();
        frameBuffer.PrepareColorCells(targetWidth, targetHeight);
        
        var cells = frameBuffer.ColorCells;
        clear.width = 1;
        cells.Fill(clear);
        
        DrawRectCommandsInternal(targetWidth, cells);
    }
#endregion
}