// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Numerics;

// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


internal sealed class GuiState
{
    private  readonly   GuiStyle        defaultStyle    = new() { color = CreateDefaultColors() };
    internal readonly   Stack<GuiStyle> revertStyles    = new();
    internal readonly   Stack<GuiStyle> stylePool       = new();
    internal readonly   GuiStyle        currentStyle    = new();
    
    internal            GuiWindow       window          = null!;
    internal            Vector2?        nextWindowPos;
    internal            Vector2?        nextWindowSize;
    
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
            SliderColor  = 0xccccccff,
            FocusColor   = 0x007affff
        };
    }
    
    internal void Reset()
    {
        currentStyle.color = defaultStyle.color; // 💪
        revertStyles.Clear();
    }
}