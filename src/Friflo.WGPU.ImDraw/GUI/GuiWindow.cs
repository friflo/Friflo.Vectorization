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



internal class GuiWindow
{
    private  readonly   Gui                 gui;
    internal            Vector2             position;
    internal            Vector2             size;
    
    internal            Vector2             cursor;
    private  readonly   Stack<int>          idStack     = new();
    private  readonly   Stack<LayoutNode>   layoutStack = new();
    private  readonly   StringBuilder       sb          = new(512,512); // => first chunk: 512 chars
    
    internal readonly   Color32             textColor   = 0x000000ff;
    internal readonly   Color32             buttonColor = 0xddddddff;
    internal readonly   Color32             buttonHover = 0xeeeeeeff;
    internal readonly   Color32             buttonDown  = 0xbbbbbbff;
    internal readonly   Color32             sliderColor = 0xccccccff;
    
    internal GuiWindow(Gui gui) {
        this.gui = gui;
    }
    
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

    internal void MoveCursor(Vector2 widgetSize)
    {
        if (layoutStack.Count > 0) {
            var layout = layoutStack.Pop();
            
            if (layout.direction == LayoutDirection.Vertical) {
                cursor.Y += widgetSize.Y + 6f;
                layout.maxSize.X = Math.Max(layout.maxSize.X, widgetSize.X);
                layout.maxSize.Y += widgetSize.Y + 6f;
            } else {
                cursor.X += widgetSize.X + 6f;
                layout.maxSize.X += widgetSize.X + 6f;
                layout.maxSize.Y = Math.Max(layout.maxSize.Y, widgetSize.Y);
            }
            layoutStack.Push(layout);
        } else {
            cursor.Y += widgetSize.Y + 6f;
        }
    }
    
    internal bool IsHoverAt(Vector2 pos, Vector2 widgetSize)
    {
        var x = gui.input.Mouse.X;
        var y = gui.input.Mouse.Y;
        bool isOverWidget = pos.X <= x && x <= pos.X + widgetSize.X &&
                            pos.Y <= y && y <= pos.Y + widgetSize.Y;
        if (!isOverWidget) {
            return false;
        }
        return gui.IsTopWindowAt(gui.input.Mouse, this);
    }

    internal bool IsHover(Vector2 widgetSize)
    {
        return IsHoverAt(cursor, widgetSize);
    }
}