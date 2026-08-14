// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Numerics;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;

internal class Window
{
    internal            Vector2     cursor;
    private  readonly   Stack<int>  idStack = new();
    
    internal readonly   Color32     textColor   = 0x000000ff;
    internal readonly   Color32     buttonColor = 0xddddddff;
    internal readonly   Color32     buttonHover = 0xeeeeeeff;
    internal readonly   Color32     buttonDown  = 0xccccccff;
    
    
    internal void ResetScope(string title)
    {
        idStack.Clear();

        int baseHash = WidgetID.CombineHash(0, title.GetHashCode());
        idStack.Push(baseHash);
    }

    internal void ClearScope()
    {
        idStack.Clear();
    }

    internal int GetCurrentScopeHash()
    {
        return idStack.Count > 0 ? idStack.Peek() : 0;
    }
    
    internal void MoveCursor(Vector2 size)
    {
        cursor.Y += size.Y + 13;
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