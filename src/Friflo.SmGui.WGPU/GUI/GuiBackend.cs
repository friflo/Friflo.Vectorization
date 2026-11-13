// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Friflo.GPU;
using Friflo.SmGui;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.SmGui;

public sealed class WgpuGuiBackend : SmGuiBackend
{
    private  readonly   GpuDevice       device;
    internal readonly   GpuSampler      samplerLinear;
    internal readonly   GpuSampler      samplerNearest;
    
    public WgpuGuiBackend(GpuDevice device) : base(new DefaultGuiAssets()) {
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
        var batch = new WgpuBatch(this, targetFormat, maxVertices) { UiType = UiType.NativeGui };
        InitBatch(batch);
        return batch;
    }
    
    public override SmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        var texture = device.CreateTexture(new GpuTextureDescriptor {
            label   = name,
            size    = [width, height],
            format  = TextureFormat.RGBA8Unorm,
            usage   = TextureUsage.TextureBinding | TextureUsage.CopyDst
        });
        texture.Write(rgbaPixels, bytesPerRow: width * 4, rowsPerImage: height);

        var view = texture.CreateView();
        return new SmTexture(texture, view.Handle);
    }

    protected override SmBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        var vertices = new Memory<Vertex2D>(new Vertex2D[vertexCount]);
        var buffer   = device.CreateBuffer(vertices, "SmBatch Vertices", BufferProfile.StaticIn, BufferType.Vertex);
        return new SmWgpuBuffer<Vertex2D>(buffer);
    }

    protected override SmBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        var indices = new Memory<uint>(new uint[indexCount]);
        var buffer  = device.CreateBuffer(indices, "SmBatch Indices", BufferProfile.StaticIn, BufferType.Index);
        return new SmWgpuBuffer<uint>(buffer);
    }
    
    public override SmTexture LoadTexture(Stream stream, string label = null, SmTextureUsage usage = SmTextureUsage.TextureBinding | SmTextureUsage.CopyDst)
    {
        var image = assets.LoadImage(stream, SmColorComponents.RedGreenBlueAlpha);

        var texture = device.CreateTexture(new GpuTextureDescriptor {
            label  = label,
            size   = [image.width, image.height],
            format = TextureFormat.RGBA8Unorm,
            usage  = (TextureUsage)usage
        });
        texture.Write(image.data, bytesPerRow: image.width * 4, rowsPerImage: image.height);
        return new SmTexture(texture, 0);
    }
    
    public override SmImageAsset GetTextureImage(SmTexture texture)
    {
        var tex = (GpuTexture)texture.native!;
        return new SmImageAsset { width = tex.Descriptor.size.width, height = tex.Descriptor.size.height, data = tex.guiPixels, name = tex.Label };
    }
}
