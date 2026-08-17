// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.




// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public readonly ref struct ZIndexScope(Draw2D draw)
{
    private readonly Draw2D    draw     = draw;

    public void Dispose() => draw.PopZIndex();
}
