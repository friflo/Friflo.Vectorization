// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// file: GuiBackend.cs
using System;
using System.IO;
using Godot;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Godot;

public sealed class GodotGuiBackend : SmGuiBackend
{
    // internal readonly   GpuSampler      samplerLinear;
    // internal readonly   GpuSampler      samplerNearest;
    
    public GodotGuiBackend(IGuiAssets assets) : base(assets) {
        // samplerLinear   = device.CreateSampler(new GpuSamplerDescriptor { label = "Linear Sampler",  magFilter = FilterMode.Linear,  minFilter = FilterMode.Linear  });
        // samplerNearest  = device.CreateSampler(new GpuSamplerDescriptor { label = "Nearest Sampler", magFilter = FilterMode.Nearest, minFilter = FilterMode.Nearest });
    }
    
    public GodotBatch CreateBatch(int maxVertices = 60_000) {
        var batch = new GodotBatch(this, maxVertices) { UiType = UiType.NativeGui };
        InitBatch(batch);
        return batch;
    }
    
    public override SmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        var pixels      = rgbaPixels.ToArray();
        var image       = Image.CreateFromData(width, height, false, Image.Format.Rgba8, pixels);
        var texture2D   = ImageTexture.CreateFromImage(image);
        image.SetName(name);
        texture2D.SetName(name);

        return texture2D.AsSmTexture();
    }

    // Godot canvas items read vertices and indices from CPU memory when adding the triangle array.
    // So there is no GPU buffer to create.
    protected override SmBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        return new GodotVertexBuffer(vertexCount);
    }

    protected override SmBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        return new GodotIndexBuffer(indexCount);
    }
    
    public override SmTexture LoadTexture(Stream stream, string label = null, SmTextureUsage usage = SmTextureUsage.TextureBinding | SmTextureUsage.CopyDst)
    {
        var asset       = assets.LoadImage(stream, SmColorComponents.RedGreenBlueAlpha);
        var image       = Image.CreateFromData(asset.width, asset.height, false, Image.Format.Rgba8, asset.data);
        var texture2D   = ImageTexture.CreateFromImage(image);
        image.SetName(label);
        texture2D.SetName(label);

        return texture2D.AsSmTexture();
    }
    
    public override SmImageAsset GetTextureImage(SmTexture texture)
    {
        var texture2D = (Texture2D)texture.native!;
        var image = texture2D.GetImage(); // returns a copy       // todo  avoid allocation
        if (image.GetFormat() != Image.Format.Rgba8) {
            image.Convert(Image.Format.Rgba8);
        }
        var pixels = image.GetData();
        return new SmImageAsset { width = image.GetWidth(), height = image.GetHeight(), data = pixels, name = image.ResourceName };
    }
}
