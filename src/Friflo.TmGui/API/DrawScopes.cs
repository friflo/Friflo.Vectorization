// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
namespace Friflo.SmGui;


public readonly ref struct ScissorScope(SmDraw draw)
{
    private readonly SmDraw    draw     = draw;

    public void Dispose() => draw.PopScissor();
}

public readonly ref struct TransformScope(SmDraw draw)
{
    private readonly SmDraw    draw     = draw;

    public void Dispose() => draw.PopTransform();
}


public readonly ref struct ZIndexScope(SmDraw draw)
{
    private readonly SmDraw    draw     = draw;

    public void Dispose() => draw.PopZIndex();
}

public readonly ref struct SamplerFilterScope(SmDraw draw)
{
    private readonly SmDraw    draw     = draw;

    public void Dispose() => draw.PopSamplerFilter();
}
