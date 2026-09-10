// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.



// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.TUI.Terminal;

internal struct MouseCursorShape
{
    internal char left;
    internal char center;
    internal char right;
    
    internal static readonly MouseCursorShape[] Cursors = new MouseCursorShape [9];
    
    
    private static void Set(MouseCursor cursor, char left, char center, char right)
    {
        Cursors[(int)cursor] = new MouseCursorShape { left = left, center = center, right =  right };
    }
    
    static MouseCursorShape ()
    {
        Set(MouseCursor.ResizeW, ' ', '◀', ' ');
        Set(MouseCursor.ResizeE, ' ', '▶', ' ');
        Set(MouseCursor.ResizeN, ' ', '▲', ' ');
        Set(MouseCursor.ResizeS, ' ', '▼', ' ');
        
        Set(MouseCursor.ResizeNW, ' ', '◤', ' ');
        Set(MouseCursor.ResizeSE, ' ', '◢', ' ');
        Set(MouseCursor.ResizeNE, ' ', '◥', ' ');
        Set(MouseCursor.ResizeSW, ' ', '◣', ' ');
    }
}
