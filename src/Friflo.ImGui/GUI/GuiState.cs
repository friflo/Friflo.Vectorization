// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Numerics;

// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


internal struct RevertStyle
{
    internal        GuiColors   colors;
    internal        GuiSizes    sizes;
    public override string      ToString() => $"colors: {colors.ToString()}  sizes: {sizes.ToString()}";
}


internal sealed class GuiState
{
    private  readonly   GuiStyle                defaultStyle        = new() { colors = CreateDefaultColors(), sizes = CreateDefaultSizes() };
    internal            RevertStyle[]           revertStyles        = [];
    internal            int                     revertStylesCount;
    internal readonly   GuiStyle                currentStyle        = new();
    internal readonly   Dictionary<int,Vector2> mouseOffsets        = new();
    
    internal            GuiWindow               window              = null!;
    
    private             int                     frameCount;
    internal            bool                    IsNewFrame          { get; private set;}

    public   override   string                  ToString()          => $"window: {window}";

    private static GuiColors CreateDefaultColors()
    {
        return new GuiColors
        {
            WindowColor  = 0xaaaaaaff,
            TextColor    = 0x000000ff,
            
            ButtonText   = 0x000000ff,
            ButtonColor  = 0xddddddff,
            ButtonHover  = 0xeeeeeeff,
            ButtonDown   = 0xbbbbbbff,
            
            SliderFill   = 0xffffffff,
            SliderColor  = 0xbbbbbbff,
            
            FocusColor   = 0x007affff
        };
    }
    
    private static GuiSizes CreateDefaultSizes()
    {
        return new GuiSizes
        {
            WindowPadding    	= new Padding2D(horizontal: 12f, vertical: 12f),
            FramePadding    	= new Padding2D(horizontal: 16f, vertical: 2f),
            ItemSpacing    		= new Padding2D(horizontal: 8f,  vertical: 4f),
            CellPadding      	= new Padding2D(horizontal: 6f,  vertical: 4f),
            ContainerPadding 	= new Padding2D(horizontal: 8f,  vertical: 8f)
        };
    }
    
    internal void Reset()
    {
        currentStyle.colors     = defaultStyle.colors; // 💪
        currentStyle.sizes    	= defaultStyle.sizes;
        revertStylesCount       = 0;
    }

    internal void SetFrameCount(int inputFrameCount)
    {
        IsNewFrame  = inputFrameCount > frameCount;
        frameCount  = inputFrameCount;
    }
}