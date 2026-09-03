// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Friflo.GPU;
using Friflo.TmGui;
using StbImageSharp;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.TmGui;

public sealed class WgpuGuiBackend : TmGuiBackend
{
    private  readonly   GpuDevice       device;
    internal readonly   GpuSampler      samplerLinear;
    internal readonly   GpuSampler      samplerNearest;
    
    public WgpuGuiBackend(GpuDevice device) {
        this.device = device;
        samplerLinear   = device.CreateSampler(new GpuSamplerDescriptor { label = "Linear Sampler",  magFilter = FilterMode.Linear,  minFilter = FilterMode.Linear  });
        samplerNearest  = device.CreateSampler(new GpuSamplerDescriptor { label = "Nearest Sampler", magFilter = FilterMode.Nearest, minFilter = FilterMode.Nearest });
    }
    
    public override void Dispose() {
        base.Dispose();
        samplerLinear.Dispose();
        samplerNearest.Dispose();
    }
    
    public WgpuBatch CreateBatch(TextureFormat targetFormat, int maxVertices = 60_000) {
        var batch = new WgpuBatch(this, targetFormat, maxVertices);
        InitBatch(batch);
        return batch;
    }
    
    protected override TmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        var texture = device.CreateTexture(new GpuTextureDescriptor {
            label   = name,
            size    = [width, height],
            format  = TextureFormat.RGBA8Unorm,
            usage   = TextureUsage.TextureBinding | TextureUsage.CopyDst
        });
        texture.Write(rgbaPixels, bytesPerRow: width * 4, rowsPerImage: height);

        var view = texture.CreateView();
        return new TmTexture(texture, view.Handle);
    }

    protected override TmBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        var vertices = new Memory<Vertex2D>(new Vertex2D[vertexCount]);
        var buffer   = device.CreateBuffer(vertices, "TmBatch Vertices", BufferProfile.StaticIn, BufferType.Vertex);
        return new TmWgpuBuffer<Vertex2D>(buffer);
    }

    protected override TmBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        var indices = new Memory<uint>(new uint[indexCount]);
        var buffer  = device.CreateBuffer(indices, "TmBatch Indices", BufferProfile.StaticIn, BufferType.Index);
        return new TmWgpuBuffer<uint>(buffer);
    }
    
    public GpuTexture LoadTexture(Stream stream, string label = null, TextureUsage usage = TextureUsage.TextureBinding | TextureUsage.CopyDst)
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
    }
}
