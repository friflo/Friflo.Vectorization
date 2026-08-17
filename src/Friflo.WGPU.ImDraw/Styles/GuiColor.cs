// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Diagnostics;
// ReSharper disable UnusedMember.Global


// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;

public enum ColorId
{
    WindowColor,
    
    TextColor,
    
    ButtonText ,
    ButtonColor,
    ButtonHover,
    ButtonDown,
    
    SliderColor,
    
    FocusColor
}


public struct GuiColor
{
    public  Color32     WindowColor     { get;  set => field = Override(ColorId.WindowColor,    value); }
    
    public  Color32     TextColor       { get;  set => field = Override(ColorId.TextColor,      value); }
    
    public  Color32     ButtonText      { get;  set => field = Override(ColorId.ButtonText,     value); }
    public  Color32     ButtonColor     { get;  set => field = Override(ColorId.ButtonColor,    value); }
    public  Color32     ButtonHover     { get;  set => field = Override(ColorId.ButtonHover,    value); }
    public  Color32     ButtonDown      { get;  set => field = Override(ColorId.ButtonDown,     value); }
    
    public  Color32     SliderColor     { get;  set => field = Override(ColorId.SliderColor,    value); }
    
    public  Color32     FocusColor      { get;  set => field = Override(ColorId.FocusColor,     value); }
     
    
    public              Bitset64<ColorId>   Overrides   => overrides;
    public  override    string              ToString()  => $"overrides: {overrides.Count}";
    
    
    public  void RemoveOverride(ColorId id)  => overrides.Remove(id);
    public  bool HasOverride(ColorId id)     => overrides.Contains(id);
    public  void ClearOverrides()            => overrides = default;
    
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] internal   Bitset64<ColorId>   overrides;
    
    private Color32 Override(ColorId id, Color32 color) {
        overrides.Add(id);
        return color;
    }
    
    internal static void ApplyOverrides(in GuiColor source, ref GuiColor target, Bitset64<ColorId> overrides)
    {
        foreach (var colorState in overrides)
        {
            switch (colorState) {
                case ColorId.WindowColor:   target.WindowColor  = source.WindowColor;   break;
                case ColorId.TextColor:     target.TextColor    = source.TextColor;     break;
                case ColorId.ButtonText:    target.ButtonText   = source.ButtonText;    break;
                case ColorId.ButtonColor:   target.ButtonColor  = source.ButtonColor;   break;
                case ColorId.ButtonHover:   target.ButtonHover  = source.ButtonHover;   break;
                case ColorId.ButtonDown:    target.ButtonDown   = source.ButtonDown;    break;
                case ColorId.SliderColor:   target.SliderColor  = source.SliderColor;   break;
                case ColorId.FocusColor:    target.FocusColor   = source.FocusColor;  	break;
            }
        }
    }
}

