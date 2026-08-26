// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


public readonly ref struct ScissorScope(Draw2D draw)
{
    private readonly Draw2D    draw     = draw;

    public void Dispose() => draw.PopScissor();
}

public readonly ref struct TransformScope(Draw2D draw)
{
    private readonly Draw2D    draw     = draw;

    public void Dispose() => draw.PopTransform();
}


public readonly ref struct ZIndexScope(Draw2D draw)
{
    private readonly Draw2D    draw     = draw;

    public void Dispose() => draw.PopZIndex();
}

public readonly ref struct SamplerFilterScope(Draw2D draw)
{
    private readonly Draw2D    draw     = draw;

    public void Dispose() => draw.PopSamplerFilter();
}
