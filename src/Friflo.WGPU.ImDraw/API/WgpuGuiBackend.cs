// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Friflo.GPU;
using Friflo.WGPU;
using StbImageSharp;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;

public sealed class WgpuGuiBackend : ImGuiBackend  
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
        samplerLinear.Dispose();
        samplerNearest.Dispose();
    }
    
    public Batch2D CreateBatch2D(WgpuGuiBackend backend, TextureFormat targetFormat, int maxVertices = 60_000) {
        return new WgpuBatch(backend, device, targetFormat, maxVertices);
    }
    
    protected internal override ImTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        var texture = device.CreateTexture(new GpuTextureDescriptor {
            label   = name,
            size    = [width, height],
            format  = TextureFormat.RGBA8Unorm,
            usage   = TextureUsage.TextureBinding | TextureUsage.CopyDst
        });
        texture.Write(rgbaPixels, bytesPerRow: width * 4, rowsPerImage: height);

        var view = texture.CreateView();
        return new ImTexture(texture, view.Handle);
    }

    protected internal override ImBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        var vertices = new Memory<Vertex2D>(new Vertex2D[vertexCount]);
        var buffer   = device.CreateBuffer(vertices, "Batch2D Vertices", BufferProfile.StaticIn, BufferType.Vertex);
        return new ImWgpuBuffer<Vertex2D>(buffer);
    }

    protected internal override ImBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        var indices = new Memory<uint>(new uint[indexCount]);
        var buffer  = device.CreateBuffer(indices, "Batch2D Indices", BufferProfile.StaticIn, BufferType.Index);
        return new ImWgpuBuffer<uint>(buffer);
    }
    
    public GpuTexture LoadTexture(Stream stream, string? label = null, TextureUsage usage = TextureUsage.TextureBinding | TextureUsage.CopyDst)
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

internal class ImWgpuBuffer<T> : ImBuffer<T> where T : unmanaged
{
    internal readonly GpuBuffer<T>  native;
    public   override Memory<T>     Memory => native.hostMemory;
    
    public ImWgpuBuffer(GpuBuffer<T> buffer) {
        native = buffer;
    }

    public override void Dispose() {
        native.Dispose();
    }
    
    public override void Write(int start, int length) {
        native.In(start, length).Write();
    }
}