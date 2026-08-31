// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
namespace Friflo.ImGui2D;


public readonly ref struct ScissorScope(ImDraw draw)
{
    private readonly ImDraw    draw     = draw;

    public void Dispose() => draw.PopScissor();
}

public readonly ref struct TransformScope(ImDraw draw)
{
    private readonly ImDraw    draw     = draw;

    public void Dispose() => draw.PopTransform();
}


public readonly ref struct ZIndexScope(ImDraw draw)
{
    private readonly ImDraw    draw     = draw;

    public void Dispose() => draw.PopZIndex();
}

public readonly ref struct SamplerFilterScope(ImDraw draw)
{
    private readonly ImDraw    draw     = draw;

    public void Dispose() => draw.PopSamplerFilter();
}
