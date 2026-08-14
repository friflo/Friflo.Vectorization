// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Numerics;


// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;

internal class Window
{
    internal            Vector2         cursor;
    internal readonly   Stack<WidgetID> idStack = new();
    
    internal readonly   Color32         textColor   = 0x000000ff;
    internal readonly   Color32         buttonColor = 0xddddddff;
    
    internal void MoveCursor()
    {
        cursor.Y += 50;
    }
}