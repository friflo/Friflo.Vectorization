// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Friflo.TmGui.Headless;

// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.TUI;


internal sealed class TuiTexture
{
    internal readonly   int         width;
    internal readonly   int         height;
    internal readonly   byte[]      data;
    internal readonly   TuiSixel    sixel;
    
    public   override   string      ToString() => $"{width} x {height}";
    
    internal TuiTexture(int width, int height, byte[] data) {
        this.width  = width;
        this.height = height;
        this.data   = data;
        sixel       = new TuiSixel(width, height, data);
    }
}

public sealed class TuiBackend : TmGuiBackend
{
    internal readonly   string  name;
    
    public   override   string  ToString()  => name;
    
    public TuiBackend(string name) : base(new TuiAssets()) {
        this.name = name;
    }

    public TuiBatch CreateBatch(TuiColorMode colorMode)
    {
        var batch = new TuiBatch(this, colorMode);
        InitBatch(batch);
        return batch;
    }
    
    protected internal override TmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        var native = new HeadlessTexture(name, width, height, rgbaPixels);
        return new TmTexture(native, 0);
    }

    protected internal override TmBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        return new TuiBuffer<Vertex2D>();
    }

    protected internal override TmBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        return new TuiBuffer<uint>();
    }
    
    public override TmTexture LoadTexture(Stream stream, string? label = null, TmTextureUsage usage = TmTextureUsage.TextureBinding | TmTextureUsage.CopyDst)
    {
        var image = assets.LoadImage(stream, TmColorComponents.RedGreenBlueAlpha);

        // texture.Write(image.data, bytesPerRow: image.width * 4, rowsPerImage: image.height);
        var tuiTexture = new TuiTexture(image.width, image.height, image.data);
        return new TmTexture(tuiTexture, 0);
    }
}
