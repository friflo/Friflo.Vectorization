// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Shaders.Imdraw;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public sealed partial class Batcher2D : IDisposable
{
    internal readonly   GpuBuffer<Vertex2D> vertexBuffer;
    private  readonly   GpuTexture          defaultWhiteTexture;
    internal readonly   GpuTextureView      defaultWhiteTextureView;
    internal readonly   GpuSampler          defaultSampler;
    internal            int                 vertexCount;
    internal            ImUniforms          uniforms;
    internal            GpuTextureView?     currentTextureView;


    public void Dispose()
    {
        vertexBuffer.Dispose();
        defaultWhiteTexture.Dispose();
        defaultSampler.Dispose();
    }

    public Batcher2D(WgpuDevice device, int maxVertices = 60_000)
    {
        vertexBuffer        = device.CreateBuffer<Vertex2D>(maxVertices, default, "Batcher2D", BufferProfile.InOut);
        defaultWhiteTexture = device.CreateTexture(new GpuTextureDescriptor {
            label   = "white1x1",
            size    = [1, 1],
            format  = TextureFormat.RGBA8Unorm,
            usage   = TextureUsage.TextureBinding | TextureUsage.CopyDst});
        
        ReadOnlySpan<byte> whitePixel = [255, 255, 255, 255];
        defaultWhiteTexture.Write(whitePixel, bytesPerRow: 4, rowsPerImage: 1, writeSize: new GpuExtent3D(1, 1, 1));
        
        defaultWhiteTextureView = defaultWhiteTexture.CreateView();
        
        defaultSampler = device.CreateSampler(new GpuSamplerDescriptor {
            magFilter = FilterMode.Linear,  // TODO  use Nearest for pixel art  
            minFilter = FilterMode.Linear   // TODO  use Nearest for pixel art
        });
    }
    
    [Shader("~/shaders/imdraw/draw2d.wgsl", vertex: "vs_main", fragment: "fs_main")]
    internal static partial void Draw(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]                   in ImUniforms       globals,
        [Map(0, 1)] [texture_2d(ST.f32)]        GpuTextureView      texture,
        [Map(0, 2)] [sampler]                   GpuSampler          sampler,
                    [VertexBuffer(0)] [Draw]    InBuffer<Vertex2D>  vertices);
    
    // [ ]  If needed, add parameter: [IndexBuffer] InBuffer<ushort|uint> indices.
}



public readonly ref struct Draw2D : IDisposable
{
    private readonly Batcher2D      batcher;
    private readonly RenderPass     pass;   // RenderPass is ref struct
    private readonly RenderConfig   config; // handle struct (size: 4 bytes)

    
    public void Dispose() { }
    
    public Draw2D(Batcher2D batcherBatcher, RenderPass pass, RenderConfig config)
    {
        batcher     = batcherBatcher;
        this.pass   = pass;
        this.config = config;
    }
    
    public void Rectangle(in Vector2 position, in Vector2 size, uint color)
    {
        DrawQuad(position, size, color);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawQuad(in Vector2 position, in Vector2 size, uint color, GpuTextureView? texture = null)
    {
        texture ??= batcher.defaultWhiteTextureView;
        
        if (batcher.vertexCount + 6 > batcher.vertexBuffer.Length || (batcher.currentTextureView != null && batcher.currentTextureView.Value.Handle != texture.Value.Handle)) {
            Flush();
        }
        batcher.currentTextureView = texture;

        var span = batcher.vertexBuffer.InOut(batcher.vertexCount, 6).Span;
        
        // fill Quad with two triangles (TL, TR, BL / TR, BR, BL)
        FillQuad(span, position, size, color);

        batcher.vertexCount += 6;
    }
    
    /// <summary>
    /// Fills a 6-vertex span representing two triangles for a quad (TL, TR, BL, TR, BR, BL).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillQuad(Span<Vertex2D> span, in Vector2 position, in Vector2 size, uint color)
    {
        float x1 = position.X;
        float y1 = position.Y;
        float x2 = position.X + size.X;
        float y2 = position.Y + size.Y;

        // V0: Top-Left
        span[0] = new Vertex2D { Position = new Vector2(x1, y1), UV = new Vector2(0f, 0f), Color = color };
        // V1: Top-Right
        span[1] = new Vertex2D { Position = new Vector2(x2, y1), UV = new Vector2(1f, 0f), Color = color };
        // V2: Bottom-Left
        span[2] = new Vertex2D { Position = new Vector2(x1, y2), UV = new Vector2(0f, 1f), Color = color };

        // V3: Top-Right (reused)
        span[3] = span[1];
        // V4: Bottom-Right
        span[4] = new Vertex2D { Position = new Vector2(x2, y2), UV = new Vector2(1f, 1f), Color = color };
        // V5: Bottom-Left (reused)
        span[5] = span[2];
    }
    
    public void SetViewport(float width, float height)
    {
        var proj = Matrix4x4.CreateOrthographicOffCenter(0f, width, height, 0f, -1f, 1f);
        batcher.uniforms = new ImUniforms { projection = proj };
    }

    public void Flush()
    {
        if (batcher.vertexCount == 0) return;

        batcher.vertexBuffer.InOut(0, batcher.vertexCount).Write();

        var texture = batcher.currentTextureView ?? batcher.defaultWhiteTextureView;
        
        Batcher2D.Draw(pass, config, batcher.uniforms, texture, batcher.defaultSampler, batcher.vertexBuffer.In(0, batcher.vertexCount));
        
        batcher.vertexCount = 0;
    }
}
