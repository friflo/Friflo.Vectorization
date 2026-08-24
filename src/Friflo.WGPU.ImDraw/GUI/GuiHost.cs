// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Numerics;

// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;

internal sealed class GuiHost
{
    internal readonly   GuiInput                        input;
    internal readonly   Dictionary<string, GuiWindow>   windows     = new();
    internal readonly   List<GuiWindow>                 windowOrder = [];
    private             GuiWindow?                      focusedWindow;

    public   override   string                          ToString()  => $"windows: {windows.Count}";

    internal GuiHost(GuiInput input) {
        this.input = input;
    }
 
    internal void Dispose()
    {
        windows.Clear();
        windowOrder.Clear();
        focusedWindow = null;
    }
    
    internal void FocusWindow(GuiWindow win)
    {
        if (focusedWindow == win) return;

        windowOrder.Remove(win);
        windowOrder.Add(win);
        focusedWindow = win;
    }
    
    internal GuiWindow? GetTopWindowAt(Vector2 screenPos)
    {
        var order = windowOrder;
        for (int i = order.Count - 1; i >= 0; i--)
        {
            var win = order[i];
            
            if (screenPos.X >= win.pos.X && screenPos.X <= win.pos.X + win.size.X &&
                screenPos.Y >= win.pos.Y && screenPos.Y <= win.pos.Y + win.size.Y)
            {
                return win; // first window from top is target
            }
        }
        return null;
    }

    internal bool IsTopWindowAt(Vector2 screenPos, GuiWindow targetWindow)
    {
        return GetTopWindowAt(screenPos) == targetWindow;
    }
}