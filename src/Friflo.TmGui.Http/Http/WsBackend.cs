// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Friflo.TmGui.Headless;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;

internal class WsBackend : TmGuiBackend
{
    public WsBackend(IGuiAssets assets) : base(assets)
    {
    }

    protected override TmBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        var buffer = new MemoryBuffer<Vertex2D>(vertexCount);
        return new HeadlessBuffer<Vertex2D>(buffer);
    }

    protected override TmBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        var buffer = new MemoryBuffer<uint>(indexCount);
        return new HeadlessBuffer<uint>(buffer);
    }

    public override TmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        throw new NotImplementedException();
    }

    public override TmTexture LoadTexture(Stream stream, string? label = null, TmTextureUsage usage = TmTextureUsage.None | TmTextureUsage.CopyDst | TmTextureUsage.TextureBinding)
    {
        throw new NotImplementedException();
    }
    
    public WsBatch CreateBatch()
    {
        var batch = new WsBatch(this, 60000);
        InitBatch(batch);
        return batch;
    }
}