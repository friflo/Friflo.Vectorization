// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.TUI;

public sealed class TuiBackend : TmGuiBackend
{
    private     int             targetWidth;
    private     int             targetHeight;
    private     TuiCell[]       cells               = [];
    private     char[]          stridedFrameBuffer  = [];
    
    public      Span<TuiCell>   Cells               => cells.             AsSpan().Slice(0,  targetWidth      * targetHeight);
    public      Span<char>      StridedFrameBuffer  => stridedFrameBuffer.AsSpan().Slice(0, (targetWidth + 1) * targetHeight);
        
    
    internal void PrepareBuffers(int width, int height)
    {
        targetWidth     = width;
        targetHeight    = height;
        int cellCount   = width * height;
        
        if (cellCount > cells.Length) {
            cells = new TuiCell[cellCount];
        }
        int charCount = (width + 1) * height;
        if (charCount > stridedFrameBuffer.Length) {
            stridedFrameBuffer  = new char[charCount];
        }
    }
    
    public TuiBatch CreateBatch() {
        var batch = new TuiBatch(this);
        InitBatch(batch);
        return batch;
    }
    
    protected internal override TmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        return default;
    }

    protected internal override TmBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        return new TuiBuffer<Vertex2D>();
    }

    protected internal override TmBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        return new TuiBuffer<uint>();
    }
    
    /* public GpuTexture LoadTexture(Stream stream, string label = null, TextureUsage usage = TextureUsage.TextureBinding | TextureUsage.CopyDst)
    {
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        var texture = device.CreateTexture(new GpuTextureDescriptor {
            label  = label,
            size   = [image.Width, image.Height],
            format = TextureFormat.RGBA8Unorm,
            usage  = usage
        });
        texture.Write(image.Data, bytesPerRow: image.Width * 4, rowsPerImage: image.Height);
        return texture;
    } */
}
