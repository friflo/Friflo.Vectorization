// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


public sealed class GuiStyle
{
    public  GuiColors   colors;
    public  GuiSizes    sizes;

    public override string ToString() => $"colors: {colors.Overrides.Count}  sizes: {sizes.Overrides.Count}";

#region internal
    internal void PushOverrides(GuiStyle style, ref RevertStyle revertStyle)
    {
        // --- Backup colors and paddings that will be changed
        // revertStyle.color.overrides = style.color.overrides;
        // GuiColor.ApplyOverrides(color, ref revertStyle.color, revertStyle.color.overrides);
        revertStyle.colors               = colors; // simply copy all colors - faster than copy each color one by one
        revertStyle.colors.overrides     = style.colors.overrides;

        // --- Apply override colors
        GuiColors.ApplyOverrides(style.colors, ref colors, revertStyle.colors.overrides);
        
        // --- padding
        revertStyle.sizes             = sizes;
        revertStyle.sizes.overrides   = style.sizes.overrides;
        
        GuiSizes.ApplyOverrides(style.sizes, ref sizes, revertStyle.sizes.overrides);
    }
    
    internal void PopOverrides(in RevertStyle revertStyle)
    {
        GuiColors.ApplyOverrides(revertStyle.colors,  ref colors,  revertStyle.colors.overrides);
        
        GuiSizes.ApplyOverrides (revertStyle.sizes,   ref sizes,   revertStyle.sizes.overrides);
    }
#endregion
}

