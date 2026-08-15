// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Numerics;

// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;

internal class Gui
{
    internal readonly   Dictionary<string, GuiWindow>  windows = new();
    
    internal            GuiWindow   window = null!;
    
    internal            Vector2?    nextWindowPos;
    internal            Vector2?    nextWindowSize;
}