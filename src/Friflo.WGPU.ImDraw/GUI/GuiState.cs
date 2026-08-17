// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Numerics;

// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


internal sealed class GuiState
{
    private  readonly   GuiStyle        defaultStyle    = CreateDefaultStyle();
    internal readonly   Stack<GuiStyle> revertStyles    = new();
    internal readonly   Stack<GuiStyle> stylePool       = new();
    internal readonly   GuiStyle        currentStyle    = new();
    
    internal            GuiWindow       window          = null!;
    internal            Vector2?        nextWindowPos;
    internal            Vector2?        nextWindowSize;

    private static readonly   Bitset64<ColorId> AllColorIds = CreateAllColorIds();
    
    private static Bitset64<ColorId> CreateAllColorIds()
    {
        var ids = new Bitset64<ColorId>();
        foreach (var colorId in Enum.GetValues<ColorId>()) {
            ids.Add(colorId);
        }
        return ids;
    }

    
    private static GuiStyle CreateDefaultStyle()
    {
        return new GuiStyle
        {
            windowColor  = 0xaaaaaaff,
            textColor    = 0x000000ff,
            buttonText   = 0x000000ff,
            buttonColor  = 0xddddddff,
            buttonHover  = 0xeeeeeeff,
            buttonDown   = 0xbbbbbbff,
            sliderColor  = 0xccccccff,
        };
    }
    
    internal void Reset()
    {
        GuiStyle.ApplyOverrides(defaultStyle, currentStyle, AllColorIds);
        revertStyles.Clear();
    }
}