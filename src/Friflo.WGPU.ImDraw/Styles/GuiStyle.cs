// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Diagnostics;

// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public sealed class GuiStyle
{
    public GuiColor color;
    
#region internal
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] internal  GuiStyle? overrideStyle; // set by revertStyle
    

    
    internal void PushOverrides(GuiStyle revertStyle)
    {
        var newStyle = revertStyle.overrideStyle!;
        
        // --- Backup colors that will be changed to revertStyle
        GuiColor.ApplyOverrides(color, ref revertStyle.color, newStyle.color.overrides);

        // --- Apply override colors
        GuiColor.ApplyOverrides(newStyle.color, ref color, newStyle.color.overrides);
    }
    
    internal void PopOverrides(GuiStyle revertStyle)
    {
        GuiColor.ApplyOverrides(revertStyle.color, ref color, revertStyle.overrideStyle!.color.overrides);
    }
#endregion
}

