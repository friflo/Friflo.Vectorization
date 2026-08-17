// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Numerics;

// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


internal sealed class GuiState
{
    private  readonly   GuiStyle        defaultStyle    = new();
    internal readonly   Stack<GuiStyle> styleStack      = new();
    internal            GuiStyle        currentStyle;
    
    internal            Vector2?        nextWindowPos;
    internal            Vector2?        nextWindowSize;

    
    internal GuiState()
    {
        currentStyle = defaultStyle;
    }
    
    internal void Reset()
    {
        currentStyle = defaultStyle;
        styleStack.Clear();
    }
}