// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Friflo.TmGui.Headless;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;

internal class WsBackend : TmGuiBackend
{
    private  readonly   TmGuiBackend                rootBackend;
    internal readonly   Dictionary<TmTexture, int>  texture2Id      = new();
    private  readonly   Dictionary<string, WsImage> stringToImage   = new();
    private  readonly   Dictionary<int,    WsImage> idToImage       = new();
    
    public WsBackend(TmGuiBackend rootBackend) : base(rootBackend.Assets)
    {
        this.rootBackend = rootBackend;
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

    public int AddTexture(TmTexture texture)
    {
        var textureId = texture2Id.Count + 1;
        texture2Id.Add(texture, textureId);
        var asset = rootBackend.GetTextureImage(texture);
        var image = new WsImage { asset = asset, texture = texture };
        idToImage.Add(textureId, image);
        if (asset.name != null) {
            stringToImage.Add(asset.name, image);
        }
        return textureId;
    }
}

internal struct WsImage
{
    internal TmImageAsset   asset;
    internal TmTexture      texture;

    public override string ToString() => $"{asset.name} - {texture}";
}