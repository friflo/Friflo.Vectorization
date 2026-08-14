// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

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
    internal Vector2            maxSize;
}



internal class Window
{
    internal            Vector2             cursor;
    private  readonly   Stack<int>          idStack     = new();
    private  readonly   Stack<LayoutNode>   layoutStack = new();
    private  readonly   StringBuilder       sb          = new(512,512); // => first chunk: 512 chars
    
    internal readonly   Color32             textColor   = 0x000000ff;
    internal readonly   Color32             buttonColor = 0xddddddff;
    internal readonly   Color32             buttonHover = 0xeeeeeeff;
    internal readonly   Color32             buttonDown  = 0xbbbbbbff;
    internal readonly   Color32             sliderColor = 0xccccccff;
    
    internal StringBuilder Builder()
    {
        sb.Clear();
        return sb;
    }

    internal void ResetScope(string title)
    {
        idStack.Clear();
        layoutStack.Clear();
        
        int baseHash = WidgetID.CombineHash(0, title.GetHashCode());
        idStack.Push(baseHash);
        
        layoutStack.Push(new LayoutNode { direction = LayoutDirection.Vertical, startCursor = cursor, maxSize = Vector2.Zero });
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
        layoutStack.Push(new LayoutNode { 
            direction   = direction, 
            startCursor = cursor, 
            maxSize     = Vector2.Zero 
        });
    }

    internal void PopLayout()
    {
        if (layoutStack.Count > 1) {
            var finishedLayout = layoutStack.Pop();
            
            cursor = finishedLayout.startCursor;

            MoveCursor(finishedLayout.maxSize);
        }
    }

    internal void MoveCursor(Vector2 size)
    {
        if (layoutStack.Count > 0) {
            var layout = layoutStack.Pop();
            
            if (layout.direction == LayoutDirection.Vertical) {
                cursor.Y += size.Y + 6f;
                layout.maxSize.X = Math.Max(layout.maxSize.X, size.X);
                layout.maxSize.Y += size.Y + 6f;
            } else {
                cursor.X += size.X + 6f;
                layout.maxSize.X += size.X + 6f;
                layout.maxSize.Y = Math.Max(layout.maxSize.Y, size.Y);
            }
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