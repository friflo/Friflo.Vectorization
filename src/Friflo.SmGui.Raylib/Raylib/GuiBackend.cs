// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Raylib;

public sealed class RaylibGuiBackend : SmGuiBackend
{
    private  readonly   GraphicsDevice  device;
    // internal readonly   GpuSampler      samplerLinear;
    // internal readonly   GpuSampler      samplerNearest;
    
    public RaylibGuiBackend(GraphicsDevice device, IGuiAssets assets) : base(assets) {
        this.device = device;
        // samplerLinear   = device.CreateSampler(new GpuSamplerDescriptor { label = "Linear Sampler",  magFilter = FilterMode.Linear,  minFilter = FilterMode.Linear  });
        // samplerNearest  = device.CreateSampler(new GpuSamplerDescriptor { label = "Nearest Sampler", magFilter = FilterMode.Nearest, minFilter = FilterMode.Nearest });
    }
    
    public RaylibBatch CreateBatch(int maxVertices = 60_000) {
        var batch = new RaylibBatch(this, maxVertices) { UiType = UiType.NativeGui };
        InitBatch(batch);
        return batch;
    }
    
    public override SmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        var texture2D = new Texture2D(device, width, height, false, SurfaceFormat.Color);
        var pixels = rgbaPixels.ToArray(); 
        texture2D.SetData(pixels);
        texture2D.Name = name;
        return texture2D.AsSmTexture();
    }

    protected override SmBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        var vertexBuffer = new VertexBuffer(device, RaylibVertexBuffer.VertexDeclaration, vertexCount, BufferUsage.None);
        return new RaylibVertexBuffer(vertexBuffer);
    }

    protected override SmBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        var indices = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indexCount, BufferUsage.None);
        return new RaylibIndexBuffer(indices);
    }
    
    public override SmTexture LoadTexture(Stream stream, string label = null, SmTextureUsage usage = SmTextureUsage.TextureBinding | SmTextureUsage.CopyDst)
    {
        var image = assets.LoadImage(stream, SmColorComponents.RedGreenBlueAlpha);
        var texture2D = new Texture2D(device, image.width, image.height, false, SurfaceFormat.Color);
        texture2D.SetData(image.data);
        texture2D.Name = label;
        return texture2D.AsSmTexture();
    }
    
    public override SmImageAsset GetTextureImage(SmTexture texture)
    {
        var texture2D = (Texture2D)texture.native!;
        var pixels = new  byte[texture2D.Width * texture2D.Height * 4]; // todo  avoid allocation
        texture2D.GetData(pixels);
        return new SmImageAsset { width = texture2D.Width, height = texture2D.Height, data = pixels, name = texture2D.Name };
    }
}
