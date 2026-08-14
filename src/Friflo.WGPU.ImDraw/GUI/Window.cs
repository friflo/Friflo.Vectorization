// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Numerics;

// ReSharper disable InvertIf
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


internal enum LayoutDirection
{
    Vertical,
    Horizontal
}

internal struct LayoutNode
{
    internal LayoutDirection    direction;
    internal Vector2            startCursor;
    internal Vector2            currentCursor;
}



internal class Window
{
    internal            Vector2             cursor;
    private  readonly   Stack<int>          idStack     = new();
    private  readonly   Stack<LayoutNode>   layoutStack = new();
    
    internal readonly   Color32             textColor   = 0x000000ff;
    internal readonly   Color32             buttonColor = 0xddddddff;
    internal readonly   Color32             buttonHover = 0xeeeeeeff;
    internal readonly   Color32             buttonDown  = 0xbbbbbbff;

    internal void ResetScope(string title)
    {
        idStack.Clear();
        layoutStack.Clear();
        
        int baseHash = WidgetID.CombineHash(0, title.GetHashCode());
        idStack.Push(baseHash);
        
        layoutStack.Push(new LayoutNode { direction = LayoutDirection.Vertical, startCursor = cursor, currentCursor = cursor });
    }

    internal void ClearScope()
    {
        idStack.Clear();
        layoutStack.Clear();
    }

    internal int GetCurrentScopeHash()
    {
        return idStack.Count > 0 ? idStack.Peek() : 0;
    }

    internal void PushLayout(LayoutDirection direction)
    {
        layoutStack.Push(new LayoutNode { direction = direction, startCursor = cursor, currentCursor = cursor });
    }

    internal void PopLayout()
    {
        if (layoutStack.Count > 1) {
            var finishedLayout = layoutStack.Pop();
            // Advance the parent cursor past the completed container block
            MoveCursor(new Vector2(finishedLayout.currentCursor.X - finishedLayout.startCursor.X, finishedLayout.currentCursor.Y - finishedLayout.startCursor.Y));
        }
    }

    internal void MoveCursor(Vector2 size)
    {
        if (layoutStack.Count > 0) {
            var layout = layoutStack.Pop();
            
            if (layout.direction == LayoutDirection.Vertical) {
                cursor.Y += size.Y + 6f;
            } else {
                cursor.X += size.X + 6f;
            }
            layout.currentCursor = cursor;

            layoutStack.Push(layout);
        } else {
            cursor.Y += size.Y + 6f;
        }
    }

    internal bool IsHover(Batch2D batch, Vector2 size)
    {
        var x = batch.input.x;
        var y = batch.input.y;
        if (cursor.X <= x && x <= cursor.X + size.X &&
            cursor.Y <= y && y <= cursor.Y + size.Y)
        {
            return true;
        }
        return false;
    }
}