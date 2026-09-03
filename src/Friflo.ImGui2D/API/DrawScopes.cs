// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public readonly ref struct ScissorScope(TmDraw draw)
{
    private readonly TmDraw    draw     = draw;

    public void Dispose() => draw.PopScissor();
}

public readonly ref struct TransformScope(TmDraw draw)
{
    private readonly TmDraw    draw     = draw;

    public void Dispose() => draw.PopTransform();
}


public readonly ref struct ZIndexScope(TmDraw draw)
{
    private readonly TmDraw    draw     = draw;

    public void Dispose() => draw.PopZIndex();
}

public readonly ref struct SamplerFilterScope(TmDraw draw)
{
    private readonly TmDraw    draw     = draw;

    public void Dispose() => draw.PopSamplerFilter();
}
