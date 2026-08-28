// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


public sealed class GuiStyle
{
    public GuiColor     color;
    public GuiPadding   padding;

    public override string ToString() => $"colors: {color.Overrides.Count}  paddings: {padding.Overrides.Count}";

#region internal
    internal void PushOverrides(GuiStyle style, ref RevertStyle revertStyle)
    {
        // --- Backup colors that will be changed
        // revertStyle.color.overrides = style.color.overrides;
        // GuiColor.ApplyOverrides(color, ref revertStyle.color, revertStyle.color.overrides);
        revertStyle.color = color; // simply copy all colors - faster than copy each color one by one
        revertStyle.color.overrides = style.color.overrides;

        // --- Apply override colors
        GuiColor.ApplyOverrides(style.color, ref color, revertStyle.color.overrides);
        
        // --- padding
        revertStyle.padding = padding;
        revertStyle.padding.overrides = style.padding.overrides;
    }
    
    internal void PopOverrides(in RevertStyle revertStyle)
    {
        GuiColor.ApplyOverrides  (revertStyle.color,   ref color,   revertStyle.color.overrides);
        
        GuiPadding.ApplyOverrides(revertStyle.padding, ref padding, revertStyle.padding.overrides);
    }
#endregion
}

