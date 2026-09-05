// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

namespace Friflo.TmGui.TUI;

public sealed class TuiBackend : TmGuiBackend
{
    public TuiBatch CreateBatch(TuiColorMode colorMode)
    {
        var batch = new TuiBatch(this, colorMode);
        InitBatch(batch);
        return batch;
    }
    
    protected internal override TmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        return default;
    }

    protected internal override TmBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        return new TuiBuffer<Vertex2D>();
    }

    protected internal override TmBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        return new TuiBuffer<uint>();
    }
}
