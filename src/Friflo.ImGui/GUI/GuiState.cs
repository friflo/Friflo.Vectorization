// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Numerics;

// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


internal struct RevertStyle
{
    internal        GuiColor    color;
    internal        GuiPadding  padding;
    public override string      ToString() => $"color: {color.ToString()}  padding: {padding.ToString()}";
}


internal sealed class GuiState
{
    private  readonly   GuiStyle                defaultStyle        = new() { color = CreateDefaultColors(), padding = CreateDefaultPaddings() };
    internal            RevertStyle[]           revertStyles        = [];
    internal            int                     revertStylesCount;
    internal readonly   GuiStyle                currentStyle        = new();
    internal readonly   Dictionary<int,Vector2> mouseOffsets        = new();
    
    internal            GuiWindow               window              = null!;
    
    private             int                     frameCount;
    internal            bool                    IsNewFrame          { get; private set;}

    public   override   string                  ToString()          => $"window: {window}";

    private static GuiColor CreateDefaultColors()
    {
        return new GuiColor
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
    
    private static GuiPadding CreateDefaultPaddings()
    {
        return new GuiPadding
        {
            WindowPadding    = new Padding2D(horizontal: 12f, vertical: 12f),
            ButtonPadding    = new Padding2D(horizontal: 12f, vertical: 6f),
            SliderPadding    = new Padding2D(horizontal: 8f,  vertical: 4f),
            CellPadding      = new Padding2D(horizontal: 6f,  vertical: 4f),
            ContainerPadding = new Padding2D(horizontal: 8f,  vertical: 8f)
        };
    }
    
    internal void Reset()
    {
        currentStyle.color      = defaultStyle.color; // 💪
        currentStyle.padding    = defaultStyle.padding;
        revertStylesCount       = 0;
    }

    internal void SetFrameCount(int inputFrameCount)
    {
        IsNewFrame  = inputFrameCount > frameCount;
        frameCount  = inputFrameCount;
    }
}