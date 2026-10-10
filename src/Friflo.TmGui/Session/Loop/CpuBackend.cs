// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Friflo.TmGui.TUI;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.Session;


public sealed class CpuBackend : TmGuiBackend
{
    private     readonly    TmGuiBackend    rootBackend;
    internal    readonly    string          backendName;
    
    public   override   string  ToString()  => backendName;

    public CpuBackend(string name) : base(new CpuAssets()) {
        backendName = name;
        rootBackend = null!;
    }
    
    public CpuBackend(string name, IGuiAssets assets) : base(assets) {
        backendName = name;
        rootBackend = null!;
    }
    
    internal CpuBackend(TmGuiBackend rootBackend) : base(rootBackend.Assets) {
        this.rootBackend    = rootBackend;
        backendName         = rootBackend.GetType().Name;
    }
    
    public TuiBatch CreateTuiBatch(TuiColorMode colorMode)
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
        if (texture.native is CpuTexture tex) {
            return new TmImageAsset { width = tex.width, height = tex.height, data = tex.data, name = tex.name };
        }
        return rootBackend.GetTextureImage(texture);
    }
}


internal sealed class CpuMemoryBuffer<T> : TmBuffer<T> where T : unmanaged
{
    public   override Memory<T>     Memory => default;
    
    public override void Dispose() {
    }
    
    public override void Write(int start, int length) {
        // <copy buffer -> GPU>
    }
}

internal sealed class CpuTexture
{
    internal readonly   int         width;
    internal readonly   int         height;
    internal readonly   byte[]      data;
    internal readonly   string      name;
    internal readonly   TuiSixel    sixel;
    
    public   override   string      ToString() => $"{width} x {height}";
    
    internal CpuTexture(int width, int height, byte[] data, string name) {
        this.width  = width;
        this.height = height;
        this.data   = data;
        this.name   = name;
        sixel       = new TuiSixel(width, height, data);
    }
}