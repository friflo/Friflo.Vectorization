// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable EmptyConstructor
// ReSharper disable RedundantOverriddenMember
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui.Headless;

public sealed class HeadlessBackend : ImGuiBackend
{
    public HeadlessBackend() {
    }
    
    public override void Dispose() {
        base.Dispose();
    }
    
    public HeadlessBatch CreateBatch(int maxVertices = 60_000) {
        return new HeadlessBatch(this, maxVertices);
    }
    
    protected internal override ImTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        var native = new HeadlessTexture(name, width, height, rgbaPixels);
        return new ImTexture(native, 0);
    }

    protected internal override ImBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        var buffer = new MemoryBuffer<Vertex2D>(vertexCount);
        return new HeadlessBuffer<Vertex2D>(buffer);
    }

    protected internal override ImBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        var buffer = new MemoryBuffer<uint>(indexCount);
        return new HeadlessBuffer<uint>(buffer);
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
