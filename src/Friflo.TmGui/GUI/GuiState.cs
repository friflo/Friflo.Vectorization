// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Numerics;
using Friflo.TmGui.TUI;

// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


internal struct RevertStyle
{
    internal        GuiColors   colors;
    internal        GuiSizes    sizes;
    public override string      ToString() => $"colors: {colors.ToString()}  sizes: {sizes.ToString()}";
}


internal sealed class GuiState
{
    private  readonly   GuiStyle                defaultStyle        = new();
    internal            RevertStyle[]           revertStyles        = [];
    internal            int                     revertStylesCount;
    internal readonly   GuiStyle                currentStyle        = new();
    internal readonly   Dictionary<int,Vector2> mouseOffsets        = new();
    
    internal            bool                    scrollAreaChanged;
    
    internal            GuiWindow               window              = null!;
    
    private             int                     frameCount;
    internal            bool                    IsNewFrame          { get; private set;}

    public   override   string                  ToString()          => $"window: {window}";
    
    internal void SetDefaultStyle(TmBatch batch)
    {
        defaultStyle.colors = CreateDefaultColors();
        if (batch is TuiBatch tuiBatch) {
            defaultStyle.sizes = new GuiSizes {
                WindowPadding   = new Padding2D(tuiBatch.CharWidth, tuiBatch.LineHeight),
                ItemSpacing     = new Vector2  (tuiBatch.CharWidth, 0)
            };
        } else {
            defaultStyle.sizes = CreateDefaultSizes();
        }
    }
    
    private static GuiSizes CreateDefaultSizes()
    {
        return new GuiSizes
        {
            WindowPadding    	= new Padding2D(horizontal: 20f, vertical: 20f),
            FramePadding    	= new Padding2D(horizontal: 16f, vertical:  2f),
            ItemSpacing    		= new Vector2  (x:          12f,        y:  6f),
            CellPadding      	= new Padding2D(horizontal:  6f, vertical:  4f),
            ContainerPadding 	= new Padding2D(horizontal:  8f, vertical:  8f)
        };
    }

    private static GuiColors CreateDefaultColors()
    {
        return new GuiColors
        {
            WindowColor     = 0xe8e9eaff, // 0xf0f1f2ff  0xf7f8f9ff
            TextColor       = 0x000000ff,
            
            ButtonText      = 0x000000ff,
            ButtonColor     = 0xffffffff,
            ButtonBorder    = 0xe0e0e0ff,
            ButtonHover     = 0xe0e0e0ff,
            ButtonDown      = 0xc0c0c0ff,
            
            SliderColor     = 0xffffffff,
            SliderBg        = 0xd8d8d8ff,
            
            FocusColor      = 0x007affff
        };
    }
    
    internal void Reset()
    {
        currentStyle.colors     = defaultStyle.colors; // 💪
        currentStyle.sizes    	= defaultStyle.sizes;
        revertStylesCount       = 0;
        scrollAreaChanged       = false;
    }

    internal void SetFrameCount(int inputFrameCount)
    {
        IsNewFrame  = inputFrameCount > frameCount;
        frameCount  = inputFrameCount;
    }
}