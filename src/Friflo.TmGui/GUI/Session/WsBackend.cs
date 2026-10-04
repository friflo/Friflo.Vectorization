// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Friflo.TmGui.Headless;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;

internal class WsBackend : TmGuiBackend
{
    private  readonly   TmGuiBackend                rootBackend;

    internal readonly   List<int>                   newTextures    = [];
    
    public WsBackend(TmGuiBackend rootBackend) : base(rootBackend.Assets)
    {
        this.rootBackend = rootBackend;
    }
    
    public    override   TmFont      DefaultFont => rootBackend.DefaultFont;

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

    public override TmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        return rootBackend.CreateTexture(name, width, height, rgbaPixels);
    }

    public override TmTexture LoadTexture(Stream stream, string? label = null, TmTextureUsage usage = TmTextureUsage.None | TmTextureUsage.CopyDst | TmTextureUsage.TextureBinding)
    {
        return rootBackend.LoadTexture(stream, label, usage);
    }
    
    public override TmImageAsset GetTextureImage(TmTexture texture)
    {
        return rootBackend.GetTextureImage(texture);
    }
    
    public WsBatch CreateBatch()  // WS_TAG
    {
        var batch = new WsBatch(this, 60000);
        InitBatch(batch);
        return batch;
    }


}

