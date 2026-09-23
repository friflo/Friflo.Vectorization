// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable EmptyConstructor
// ReSharper disable RedundantOverriddenMember
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.Headless;

public sealed class HeadlessBackend : TmGuiBackend
{
    public HeadlessBackend() : base(new HeadlessAssets()) {
    }
    
    public override void Dispose() {
        base.Dispose();
    }
    
    public HeadlessBatch CreateBatch(int maxVertices = 60_000) {
        var batch = new HeadlessBatch(this, maxVertices);
        InitBatch(batch);
        return batch;
    }
    
    public override TmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        var native = new HeadlessTexture(name, width, height, rgbaPixels);
        return new TmTexture(native, 0);
    }

    protected internal override TmBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        var buffer = new MemoryBuffer<Vertex2D>(vertexCount);
        return new HeadlessBuffer<Vertex2D>(buffer);
    }

    protected internal override TmBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        var buffer = new MemoryBuffer<uint>(indexCount);
        return new HeadlessBuffer<uint>(buffer);
    }
    
    public override TmTexture LoadTexture(Stream stream, string? label = null, TmTextureUsage usage = TmTextureUsage.TextureBinding | TmTextureUsage.CopyDst)
    {
        throw new NotSupportedException();
    }
}
