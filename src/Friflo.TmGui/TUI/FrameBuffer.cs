// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Text;

namespace Friflo.TmGui.TUI;

/// <summary>
/// Can be used among multiple sessions if all sessions are handled in a single thread. 
/// </summary>
public sealed class FrameBuffer
{
    private     int                 bufferWidth;
    private     int                 bufferHeight;
    private     TuiColorCell[]      colorCells              = [];
    
    public      Span<TuiColorCell>  ColorCells              => colorCells. AsSpan().Slice(0,  bufferWidth * bufferHeight);
    
    internal void PrepareColorCells(int width, int height)
    {
        bufferWidth     = width;
        bufferHeight    = height;
        var cellCount   = width * height;
        
        if (cellCount > colorCells.Length) {
            colorCells = new TuiColorCell[cellCount];
        }
    }
    
    public void SetCell(int x, int y, TuiColorCell cell)
    {
        if (0 <= x && x < bufferWidth && 0 <= y && y < bufferHeight) {
            colorCells[y * bufferWidth + x] = cell;
        }
    }
    
    public string CellsToString(ReadOnlySpan<char> lineEnd)
    {
        var sb = new StringBuilder();
        var width   = bufferWidth;
        var height  = bufferHeight;
        var cells   = ColorCells;

        for (int line = 0; line < height; line++) {
            for (int col = 0; col < width; col++) {
                var rune = cells[line * width + col].rune;
                if (rune.Value == 0) {
                    continue; // Skip ghost cells following runes which cover two cells
                }
                sb.Append(cells[line * width + col].rune);
            }
            sb.Append(lineEnd);
        }
        return sb.ToString();
    }
}
