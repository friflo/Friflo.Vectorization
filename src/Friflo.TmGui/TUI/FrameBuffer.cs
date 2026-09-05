// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

namespace Friflo.TmGui.TUI;

/// <summary>
/// Can be used among multiple sessions if all sessions are handled in a single thread. 
/// </summary>
public sealed class FrameBuffer
{
    private     int                 bufferWidth;
    private     int                 bufferHeight;
    private     TuiColorCell[]      colorCells      = [];
    private     char[]              charCells       = [];
    
    public      Span<TuiColorCell>  ColorCells      => colorCells. AsSpan().Slice(0,  bufferWidth * bufferHeight);
    public      Span<char>          CharCells       => charCells.  AsSpan().Slice(0,  bufferWidth * bufferHeight);
        
    
    internal void PrepareColorCells(int width, int height)
    {
        bufferWidth     = width;
        bufferHeight    = height;
        var cellCount   = width * height;
        
        if (cellCount > colorCells.Length) {
            colorCells = new TuiColorCell[cellCount];
        }
    }
    
    internal void PrepareCharCells(int width, int height)
    {
        bufferWidth     = width;
        bufferHeight    = height;
        var cellCount   = width * height;
        
        if (cellCount > charCells.Length) {
            charCells = new char[cellCount];
        }
    }
}
