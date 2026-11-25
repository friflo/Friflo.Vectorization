// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using SkiaSharp;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.SkiaSharp;

public sealed class SkiaSharpGuiBackend : SmGuiBackend
{
    public SkiaSharpGuiBackend(IGuiAssets assets) : base(assets) { }
    
    public SkiaSharpBatch CreateBatch(int maxVertices = 60_000) {
        var batch = new SkiaSharpBatch(this, maxVertices) { UiType = UiType.NativeGui };
        InitBatch(batch);
        return batch;
    }
    
    public override SmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        // NOTE!  Using SKAlphaType.Premul resulted rendering glyphs with visible rectangle.
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        int length = Math.Min(rgbaPixels.Length, bitmap.ByteCount);
        rgbaPixels[..length].CopyTo(bitmap.GetPixelSpan());
        return bitmap.AsSmTexture();
    }

    protected override SmBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        return new SkiaSharpVertexBuffer(vertexCount);
    }

    protected override SmBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        return new SkiaSharpIndexBuffer(indexCount);
    }
    
    public override SmTexture LoadTexture(Stream stream, string label = null, SmTextureUsage usage = SmTextureUsage.TextureBinding | SmTextureUsage.CopyDst)
    {
        var image = assets.LoadImage(stream, SmColorComponents.RedGreenBlueAlpha);
        // TODO  check SKAlphaType.Unpremul is correct (was: SKAlphaType.Premul)
        var bitmap = new SKBitmap(image.width, image.height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        
        int length = image.data.Length;
        Marshal.Copy(image.data, 0, bitmap.GetPixels(), length);
        return bitmap.AsSmTexture();
    }
    
    public override SmImageAsset GetTextureImage(SmTexture texture)
    {
        var bitmap = (SKBitmap)texture.native!;
        var length = bitmap.RowBytes * bitmap.Height;
        var pixels = new byte[length];                          // todo  avoid allocation
        Marshal.Copy(bitmap.GetPixels(), pixels, 0, length);
        return new SmImageAsset { width = bitmap.Width, height = bitmap.Height, data = pixels, name = string.Empty };
    }
}