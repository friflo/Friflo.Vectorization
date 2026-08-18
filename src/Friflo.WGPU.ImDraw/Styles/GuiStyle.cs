// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public sealed class GuiStyle
{
    public GuiColor color;

    public override string ToString() => $"colors: {color.Overrides.Count}";

#region internal
    internal void PushOverrides(GuiStyle style, ref RevertStyle revertStyle)
    {
        revertStyle.color.overrides = style.color.overrides;
        
        // --- Backup colors that will be changed to revertStyle
        GuiColor.ApplyOverrides(color, ref revertStyle.color, revertStyle.color.overrides);

        // --- Apply override colors
        GuiColor.ApplyOverrides(style.color, ref color, revertStyle.color.overrides);
    }
    
    internal void PopOverrides(in RevertStyle revertStyle)
    {
        GuiColor.ApplyOverrides(revertStyle.color, ref color, revertStyle.color.overrides);
    }
#endregion
}

