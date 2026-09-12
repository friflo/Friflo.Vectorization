// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
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
                ItemSpacing     = new Vector2  (tuiBatch.CharWidth, 0),
                TrackThickness  = new Vector2  (tuiBatch.CharWidth, tuiBatch.LineHeight)
            };
        } else {
            defaultStyle.sizes = CreateDefaultSizes(batch);
        }
    }
    
    private static GuiSizes CreateDefaultSizes(TmBatch batch)
    {
        // Snap sizes to integral values to ensure pixel accuracy
        var pt = MathF.Floor(batch.currentFont.lineHeight / 20f);
        pt = pt < 1 ? 1: pt;
        var sizes = new GuiSizes {
            WindowPadding    	= new Padding2D(horizontal: 10 * pt, vertical: 10 * pt),
            FramePadding    	= new Padding2D(horizontal:  8 * pt, vertical:  1 * pt),
            ItemSpacing    		= new Vector2  (x:           6 * pt,        y:  3 * pt),
            CellPadding      	= new Padding2D(horizontal:  3 * pt, vertical:  2 * pt),
            ContainerPadding 	= new Padding2D(horizontal:  4 * pt, vertical:  4 * pt),
            TrackThickness      = new Vector2  (x:          10 * pt,        y: 10 * pt),
        };
        return sizes;
    }

    private static GuiColors CreateDefaultColors()
    {
        return new GuiColors
        {
            WindowColor     = 0xf0f1f2ff, // 0xe8e9eaff 0xf0f1f2ff  0xf7f8f9ff
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