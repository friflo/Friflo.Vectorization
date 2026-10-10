// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Friflo.TmGui.TUI;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable EmptyConstructor
// ReSharper disable RedundantOverriddenMember
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.Headless;

public sealed class CpuBackend : TmGuiBackend
{
    private  readonly   TmGuiBackend    rootBackend;
    internal readonly   string          backendName;
    
    public   override   string          ToString()  => backendName;
    
    /// <summary>
    /// Use a <see cref="TuiAssets"/> instance for a pure TUI use.<br/>
    /// Drawing Sixel graphics in a TUI requires a <c>DefaultGuiAssets</c> instance from package <c>Friflo.TmGui.Assets</c>.
    /// </summary>
    public CpuBackend(string name, IGuiAssets assets) : base(assets) {
        backendName = name;
        rootBackend = null!;
    }
    
    internal CpuBackend(TmGuiBackend rootBackend) : base(rootBackend.Assets) {
        this.rootBackend    = rootBackend;
        backendName         = rootBackend.GetType().Name;
    }
    
    public override void Dispose() {
        base.Dispose();
    }
    
    public CpuGuiBatch CreateGuiBatch(int maxVertices = 60_000) {
        var batch = new CpuGuiBatch(this, maxVertices);
        InitBatch(batch);
        return batch;
    }
    
    public TuiBatch CreateTuiBatch(TuiColorMode colorMode)
    {
        var batch = new TuiBatch(this, colorMode);
        InitBatch(batch);
        return batch;
    }
    
    protected internal override TmBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        return new CpuMemoryBuffer<Vertex2D>(vertexCount);
    }

    protected internal override TmBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        // index buffer if specific for GPU. CPU utilize only vertex buffer
        return new CpuMemoryBuffer<uint>();
    }
    
    
    public override TmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        var array = rgbaPixels.ToArray();
        var length = width * height * 4;
        if (array.Length < length) {
            throw new InvalidOperationException($"texture array too small. Was: {array.Length}. Requires: {length} width: {width} height: {height}");
        }
        var native = new CpuTexture(name, width, height, array);
        return new TmTexture(native, 0);
    }
    
    public override TmTexture LoadTexture(Stream stream, string? label = null, TmTextureUsage usage = TmTextureUsage.TextureBinding | TmTextureUsage.CopyDst)
    {
        var image = assets.LoadImage(stream, TmColorComponents.RedGreenBlueAlpha);

        // texture.Write(image.data, bytesPerRow: image.width * 4, rowsPerImage: image.height);
        var cpuTexture = new CpuTexture(label!, image.width, image.height, image.data);
        return new TmTexture(cpuTexture, 0);
    }
    
    public override TmImageAsset GetTextureImage(TmTexture texture)
    {
        if (texture.native is CpuTexture tex) {
            return new TmImageAsset { width = tex.width, height = tex.height, data = tex.rgbaPixels, name = tex.name };
        }
        return rootBackend.GetTextureImage(texture);
    }
}
