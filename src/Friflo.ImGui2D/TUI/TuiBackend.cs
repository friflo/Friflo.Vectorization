// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui2D.TUI;

public sealed class TuiBackend : ImGuiBackend
{
    internal            TuiCell[]   cells;
    internal readonly   int         terminalWidth;
    internal readonly   int         terminalHeight;
        
    public TuiBackend(int terminalWidth, int terminalHeight) {
        this.terminalWidth  = terminalWidth;
        this.terminalHeight = terminalHeight;
        cells               = new TuiCell[terminalWidth * terminalHeight];
    }
    
    public TuiBatch CreateBatch() {
        return new TuiBatch(this);
    }
    
    protected internal override ImTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        return default;
    }

    protected internal override ImBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        return new TuiBuffer<Vertex2D>();
    }

    protected internal override ImBuffer<uint> CreateIndexBuffer(int indexCount)
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
