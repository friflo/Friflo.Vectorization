// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ReplaceSliceWithRangeIndexer
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.TUI;

public sealed class TuiBackend : TmGuiBackend
{
    private     int                 targetWidth;
    private     int                 targetHeight;
    private     TuiColorCell[]      colorCells          = [];
    private     TuiColorCell[]      frameBufferColor    = [];
    private     char[]              charCells           = [];
    private     char[]              frameBuffer         = [];
    
    internal    Span<TuiColorCell>  ColorCells          => colorCells.      AsSpan().Slice(0,  targetWidth      * targetHeight);
    internal    Span<char>          CharCells           => charCells.       AsSpan().Slice(0,  targetWidth      * targetHeight);
    
    public      Span<TuiColorCell>  FrameBufferColor    => frameBufferColor.AsSpan().Slice(0, (targetWidth + 2) * targetHeight);
    public      Span<char>          FrameBuffer         => frameBuffer.     AsSpan().Slice(0, (targetWidth + 2) * targetHeight);
        
    
    internal void PrepareBuffersColor(int width, int height)
    {
        targetWidth     = width;
        targetHeight    = height;
        int cellCount   = width * height;
        
        if (cellCount > colorCells.Length) {
            colorCells = new TuiColorCell[cellCount];
        }
        int charCount = (width + 2) * height;
        if (charCount > frameBufferColor.Length) {
            frameBufferColor  = new TuiColorCell[charCount];
        }
    }
    
    internal void PrepareBuffersChar(int width, int height)
    {
        targetWidth     = width;
        targetHeight    = height;
        int cellCount   = width * height;
        
        if (cellCount > charCells.Length) {
            charCells = new char[cellCount];
        }
        int charCount = (width + 2) * height;
        if (charCount > frameBuffer.Length) {
            frameBuffer  = new char[charCount];
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
