// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;

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
    internal readonly   string  backendName;
    
    public   override   string  ToString()  => backendName;
    
    public TuiBackend(string name) : base(new TuiAssets()) {
        backendName = name;
    }

    public TuiBatch CreateBatch(TuiColorMode colorMode)
    {
        var batch = new TuiBatch(this, colorMode);
        InitBatch(batch);
        return batch;
    }
    
    public override TmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)   // TODO  use byte[]
    {
        var array = rgbaPixels.ToArray();
        var length = width * height * 4;
        if (array.Length < length) {
            throw new InvalidOperationException($"texture array too small. Was: {array.Length}. Requires: {length} width: {width} height: {height}");
        }
        var tuiTexture = new TuiTexture(width, height, array);
        return new TmTexture(tuiTexture, 0);
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
