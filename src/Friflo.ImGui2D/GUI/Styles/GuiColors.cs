// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using static System.Diagnostics.DebuggerBrowsableState;
using Browse = System.Diagnostics.DebuggerBrowsableAttribute;

// ReSharper disable once CheckNamespace
namespace Friflo.ImGui2D;

public enum ColorId
{
    WindowColor,
    
    TextColor,
    
    ButtonText ,
    ButtonColor,
    ButtonBorder,
    ButtonHover,
    ButtonDown,
    
    SliderFill,
    SliderColor,
    
    FocusColor
}


public struct GuiColors
{
    public Color32  WindowColor     { readonly get => windowColor;   set => windowColor  = Add(ColorId.WindowColor,  value); }
    
    public Color32  TextColor       { readonly get => textColor;     set => textColor    = Add(ColorId.TextColor,    value); }
    
    public Color32  ButtonText      { readonly get => buttonText;    set => buttonText   = Add(ColorId.ButtonText,   value); }
    public Color32  ButtonColor     { readonly get => buttonColor;   set => buttonColor  = Add(ColorId.ButtonColor,  value); }
    public Color32  ButtonBorder    { readonly get => buttonBorder;  set => buttonBorder = Add(ColorId.ButtonBorder, value); }
    public Color32  ButtonHover     { readonly get => buttonHover;   set => buttonHover  = Add(ColorId.ButtonHover,  value); }
    public Color32  ButtonDown      { readonly get => buttonDown;    set => buttonDown   = Add(ColorId.ButtonDown,   value); }
    
    public Color32  SliderFill      { readonly get => sliderFill;    set => sliderFill   = Add(ColorId.SliderFill,   value); }
    public Color32  SliderColor     { readonly get => sliderColor;   set => sliderColor  = Add(ColorId.SliderColor,  value); }
    
    public Color32  FocusColor      { readonly get => focusColor;    set => focusColor   = Add(ColorId.FocusColor,   value); }
    
    public Color32  ScrollTrackBg       => 0xffffff00; // transparent
    public Color32  ScrollThumb         => 0xd0d0d0ff;
    public Color32  ScrollThumbActive   => 0x999999ff;
    public Color32  ScrollThumbHover    => 0xaaaaaaff;
    
    public Color32  ScrollAreaColor     => 0x00000000;
     
    
    public  readonly            Bitset64<ColorId>   Overrides                   => overrides;
    public  readonly override   string              ToString()                  => $"overrides: {overrides.Count}";
    
    public                      void                RemoveOverride(ColorId id)          => overrides.Remove(id);
    public  readonly            bool                HasOverride   (ColorId id)          => overrides.Contains(id);
    public                      void                ClearOverrides()                    => overrides = default;
    
#region internal
    [Browse(Never)] private     Color32 windowColor;
    
    [Browse(Never)] private     Color32 textColor;
    
    [Browse(Never)] private     Color32 buttonText;
    [Browse(Never)] private     Color32 buttonColor;
    [Browse(Never)] private     Color32 buttonBorder;
    [Browse(Never)] private     Color32 buttonHover;
    [Browse(Never)] private     Color32 buttonDown;
    
    [Browse(Never)] private     Color32 sliderFill;
    [Browse(Never)] private     Color32 sliderColor;
    
    [Browse(Never)] private     Color32 focusColor;
    
    [Browse(Never)] internal    Bitset64<ColorId>   overrides;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Color32 Add(ColorId id, Color32 color) {
        // same as:  overrides.Add(id);
        overrides.value |= 1UL << Unsafe.As<ColorId, int>(ref id);
        return color;
    }
    
    internal static void ApplyOverrides(in GuiColors source, ref GuiColors target, Bitset64<ColorId> overrides)
    {
        foreach (var colorState in overrides)
        {
            switch (colorState) {
                case ColorId.WindowColor:   target.windowColor  = source.windowColor;   break;
                case ColorId.TextColor:     target.textColor    = source.textColor;     break;
                case ColorId.ButtonText:    target.buttonText   = source.buttonText;    break;
                case ColorId.ButtonColor:   target.buttonColor  = source.buttonColor;   break;
                case ColorId.ButtonBorder:  target.buttonBorder = source.buttonBorder;  break;
                case ColorId.ButtonHover:   target.buttonHover  = source.buttonHover;   break;
                case ColorId.ButtonDown:    target.buttonDown   = source.buttonDown;    break;
                case ColorId.SliderFill:    target.sliderFill   = source.sliderFill;    break;
                case ColorId.SliderColor:   target.sliderColor  = source.sliderColor;   break;
                case ColorId.FocusColor:    target.focusColor   = source.focusColor;  	break;
            }
        }
    }
#endregion
    
    public void AddOverrides(in GuiColors source)
    {
        foreach (var colorState in source.overrides)
        {
            switch (colorState) {
                case ColorId.WindowColor:   WindowColor  = source.windowColor;   break;
                case ColorId.TextColor:     TextColor    = source.textColor;     break;
                case ColorId.ButtonText:    ButtonText   = source.buttonText;    break;
                case ColorId.ButtonColor:   ButtonColor  = source.buttonColor;   break;
                case ColorId.ButtonBorder:  ButtonBorder = source.buttonBorder;  break;
                case ColorId.ButtonHover:   ButtonHover  = source.buttonHover;   break;
                case ColorId.ButtonDown:    ButtonDown   = source.buttonDown;    break;
                case ColorId.SliderFill:    SliderFill   = source.sliderFill;    break;
                case ColorId.SliderColor:   SliderColor  = source.sliderColor;   break;
                case ColorId.FocusColor:    FocusColor   = source.focusColor;    break;
            }
        }
    }
    
    public readonly Color32 ButtonState(WidgetState widgetState)
    {
        return widgetState switch {
            WidgetState.Down    => ButtonDown,
            WidgetState.Hover   => ButtonHover,
            _                   => ButtonColor
        };
    }
    
    public readonly Color32 ButtonState(DragState widgetState)
    {
        return widgetState switch {
            DragState.Down  => ButtonDown,
            DragState.Hover => ButtonHover,
            _               => ButtonColor
        };
    }
}

