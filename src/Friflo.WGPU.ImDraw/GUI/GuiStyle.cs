// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Diagnostics;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public enum ColorId
{
    windowColor,
    textColor,  
    buttonText ,
    buttonColor,
    buttonHover,
    buttonDown,
    sliderColor
}


public sealed class GuiStyle
{
    public  Color32     windowColor     { get;  set => field = Override(ColorId.windowColor,    value); }
    
    public  Color32     textColor       { get;  set => field = Override(ColorId.textColor,      value); }
    
    public  Color32     buttonText      { get;  set => field = Override(ColorId.buttonText,     value); }
    public  Color32     buttonColor     { get;  set => field = Override(ColorId.buttonColor,    value); }
    public  Color32     buttonHover     { get;  set => field = Override(ColorId.buttonHover,    value); }
    public  Color32     buttonDown      { get;  set => field = Override(ColorId.buttonDown,     value); }
    
    public  Color32     sliderColor     { get;  set => field = Override(ColorId.sliderColor,    value); }
    
    
    public              Bitset64<ColorId>   Overrides   => overrides;
    public  override    string              ToString()  => $"overrides: {overrides.Count}";
    
    
    public  void RemoveOverride(ColorId id)  => overrides.Remove(id);
    public  bool HasOverride(ColorId id)     => overrides.Contains(id);
    public  void ClearOverrides()            => overrides = default;
    
    
#region internal
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private   Bitset64<ColorId>   overrides;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] internal  GuiStyle?           overrideStyle; // set by revertStyle
    

    private Color32 Override(ColorId id, Color32 color)
    {
        overrides.Add(id);
        return color;
    }
    
    internal static void ApplyOverrides(GuiStyle source, GuiStyle target, Bitset64<ColorId> overrides)
    {
        foreach (var colorState in overrides)
        {
            switch (colorState) {
                case ColorId.windowColor:   target.windowColor   = source.windowColor;  break;
                case ColorId.textColor:     target.textColor     = source.textColor;    break;
                case ColorId.buttonText:    target.buttonText    = source.buttonText;   break;
                case ColorId.buttonColor:   target.buttonColor   = source.buttonColor;  break;
                case ColorId.buttonHover:   target.buttonHover   = source.buttonHover;  break;
                case ColorId.buttonDown:    target.buttonDown    = source.buttonDown;   break;
                case ColorId.sliderColor:   target.sliderColor   = source.sliderColor;  break;
            }
        }
    }
    
    internal void PushOverrides(GuiStyle revertStyle)
    {
        var newStyle = revertStyle.overrideStyle!;
        // --- Backup colors that will be changed to revertStyle
        ApplyOverrides(this, revertStyle, newStyle.overrides);

        // --- Apply override colors
        ApplyOverrides(newStyle, this, newStyle.overrides);
    }
    
    internal void PopOverrides(GuiStyle revertStyle)
    {
        ApplyOverrides(revertStyle, this, revertStyle.overrideStyle!.overrides);
    }
#endregion
}