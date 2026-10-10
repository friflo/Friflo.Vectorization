// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

/* TuiBackend is obsolete - replaced by CpuBackend
 
using System;
using System.IO;
using Friflo.TmGui.Session;

// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.TUI;



public sealed class TuiBackend : TmGuiBackend
{
    internal readonly   string  backendName;
    
    public   override   string  ToString()  => backendName;

    public TuiBackend(string name) : this(name, new CpuAssets()) {
        backendName = name;
    }

    public TuiBackend(string name, IGuiAssets assets) : base(assets) {
        backendName = name;
    }

    public TuiBatch CreateBatch(TuiColorMode colorMode)
    {
        var batch = new TuiBatch(this, colorMode);
        InitBatch(batch);
        return batch;
    }
    
    protected internal override TmBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        return new CpuMemoryBuffer<Vertex2D>();
    }

    protected internal override TmBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        // no index buffer used for TUI
        return new CpuMemoryBuffer<uint>();
    }
    
    
    public override TmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)   // TODO  use byte[]
    {
        var array = rgbaPixels.ToArray();
        var length = width * height * 4;
        if (array.Length < length) {
            throw new InvalidOperationException($"texture array too small. Was: {array.Length}. Requires: {length} width: {width} height: {height}");
        }
        var cpuTexture = new CpuTexture(width, height, array, name);
        return new TmTexture(cpuTexture, 0);
    }
    
    public override TmTexture LoadTexture(Stream stream, string? label = null, TmTextureUsage usage = TmTextureUsage.TextureBinding | TmTextureUsage.CopyDst)
    {
        var image = assets.LoadImage(stream, TmColorComponents.RedGreenBlueAlpha);

        // texture.Write(image.data, bytesPerRow: image.width * 4, rowsPerImage: image.height);
        var cpuTexture = new CpuTexture(image.width, image.height, image.data, label!);
        return new TmTexture(cpuTexture, 0);
    }
    
    public override TmImageAsset GetTextureImage(TmTexture texture)
    {
        var tex = (CpuTexture)texture.native!;
        return new TmImageAsset { width = tex.width, height = tex.height, data = tex.data, name = tex.name };
    }
}

*/