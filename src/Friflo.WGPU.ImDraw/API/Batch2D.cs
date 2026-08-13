// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Shaders.Imdraw;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public sealed partial class Batch2D : IDisposable
{
    internal readonly   RenderConfig        config;
    internal readonly   GpuBuffer<Vertex2D> vertexBuffer;
    internal readonly   GpuBuffer<ushort>   indexBuffer;
    private  readonly   GpuTexture          defaultWhiteTexture;
    internal readonly   GpuTextureView      defaultWhiteTextureView; // views are owned / disposed by their texture
    internal readonly   GpuSampler          samplerLinear;           // the default sampler
    internal readonly   GpuSampler          samplerNearest;
    internal readonly   GpuDevice           device;
    internal            GpuSampler          currentSampler;
    internal            Font?               defaultFont;
    internal            Vector2             viewport;
    internal            ImUniforms          uniforms;
    internal            int                 vertexStart; // start of next Draw()
    internal            int                 vertexCount;
    internal            GpuTextureView?     currentTextureView;


    public void Dispose()
    {
        defaultFont?.Dispose();
        vertexBuffer.Dispose();
        indexBuffer.Dispose();
        defaultWhiteTexture.Dispose();
        samplerLinear.Dispose();
        samplerNearest.Dispose();
    }
    
    /// <summary>
    /// Core constructor supporting a fully custom GpuSamplerDescriptor (or default Linear sampler if null).
    /// </summary>
    public Batch2D(
        GpuDevice               device,
        TextureFormat           targetFormat,
        int                     maxVertices         = 60_000)
    {
        this.device    = device;
        
        // --- vertex & index buffer - to draw quads
        int maxQuads   = maxVertices / 4;
        int maxIndices = maxQuads * 6;

        vertexBuffer = device.CreateBuffer<Vertex2D>(maxVertices, default, "Batch2D Vertices", BufferProfile.StaticIn, BufferType.Vertex);

        // generate quad indexes only once
        var indices = new ushort[maxIndices];
        for (int i = 0, v = 0; i < maxIndices; i += 6, v += 4)
        {
            indices[i + 0] = (ushort)(v + 0);
            indices[i + 1] = (ushort)(v + 1);
            indices[i + 2] = (ushort)(v + 2);
            indices[i + 3] = (ushort)(v + 2);
            indices[i + 4] = (ushort)(v + 3);
            indices[i + 5] = (ushort)(v + 0);
        }
        indexBuffer = device.CreateBuffer(indices, "Batch2D Indices", BufferProfile.StaticIn, BufferType.Index);
        indexBuffer.In().Write();
        
        // --- Texture
        defaultWhiteTexture = device.CreateTexture(new GpuTextureDescriptor {
            label   = "white1x1",
            size    = [1, 1],
            format  = TextureFormat.RGBA8Unorm,
            usage   = TextureUsage.TextureBinding | TextureUsage.CopyDst});
        
        ReadOnlySpan<byte> whitePixel = [255, 255, 255, 255];
        defaultWhiteTexture.Write(whitePixel, bytesPerRow: 4, rowsPerImage: 1, writeSize: new GpuExtent3D(1, 1, 1));
        
        defaultWhiteTextureView = defaultWhiteTexture.CreateView();
        
        samplerLinear  = device.CreateSampler(new GpuSamplerDescriptor { label = "Linear Sampler",  magFilter = FilterMode.Linear,  minFilter = FilterMode.Linear  });
        samplerNearest = device.CreateSampler(new GpuSamplerDescriptor { label = "Nearest Sampler", magFilter = FilterMode.Nearest, minFilter = FilterMode.Nearest });
        currentSampler = samplerLinear;
        
        var desc = new GpuRenderPipelineDescriptor();
        desc.VertexState.buffers = [
            new GpuVertexBufferLayout {     // [VertexBuffer(0)]   (slot: 0)
                arrayStride = Unsafe.SizeOf<Vertex2D>(),
                attributes = [
                    new GpuVertexAttribute { shaderLocation = 0, offset =  0, format = VertexFormat.Float32x2 },    // Vertex2D.position 
                    new GpuVertexAttribute { shaderLocation = 1, offset =  8, format = VertexFormat.Float32x2 },    // Vertex2D.uv
                    new GpuVertexAttribute { shaderLocation = 2, offset = 16, format = VertexFormat.Unorm8x4  }     // Vertex2D.color
                ]
        }];
        desc.FragmentState = new GpuFragmentState{ targets = [
            new GpuColorTargetState {
                format = targetFormat, // TextureFormat.BGRA8Unorm / RGBA8Unorm
                blend  = new GpuBlendState {
                    color = new GpuBlendComponent { srcFactor = BlendFactor.SrcAlpha, dstFactor = BlendFactor.OneMinusSrcAlpha, operation = BlendOperation.Add },
                    alpha = new GpuBlendComponent { srcFactor = BlendFactor.One,      dstFactor = BlendFactor.OneMinusSrcAlpha, operation = BlendOperation.Add }
                }
            }
        ]};
        desc.PrimitiveState = new GpuPrimitiveState {
            topology    = PrimitiveTopology.TriangleList,
            cullMode    = CullMode.None
        };
        config = desc.CreateConfig("Batch2D Config");
    }
    
    public Draw2D BeginDraw2D(in RenderFrame frame, in GpuRenderPassDescriptor descriptor)
    {
        descriptor.colorAttachments[0].view = frame.View;
        
        var pass = frame.BeginRenderPass(descriptor);
        
        // reset batcher state
        vertexStart         = 0;
        vertexCount         = 0;
        currentTextureView  = null;
        currentSampler      = samplerLinear;
        viewport            = new Vector2(frame.Width, frame.Height);
        
        var draw = new Draw2D(this, pass);
        draw.SetViewport(frame.Width, frame.Height);
        return draw;
    }
    
    [Shader("~/shaders/imdraw/draw2d.wgsl", vertex: "vs_main", fragment: "fs_main")]
    internal static partial void Draw(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]               in ImUniforms       globals,
        [Map(0, 1)] [texture_2d(ST.f32)]    GpuTextureView      texture,
        [Map(0, 2)] [sampler]               GpuSampler          sampler,
                    [VertexBuffer(0)]       InBuffer<Vertex2D>  vertices,
                    [IndexBuffer]   [Draw]  InBuffer<ushort>    indices);

    
    public Font GetDefaultFont()
    {
        if (defaultFont != null) {
            return defaultFont;
        }
        using var fontAtlas = typeof(Batch2D).Assembly.GetManifestResourceStream("Friflo.WGPU.ImDraw.fonts.arial-48-latin_0.png");
        using var fntFile   = typeof(Batch2D).Assembly.GetManifestResourceStream("Friflo.WGPU.ImDraw.fonts.arial-48-latin.fnt");
        using var reader    = new StreamReader(fntFile!, Encoding.UTF8);
        var fntContent      = reader.ReadToEnd();
        
        return defaultFont = Font.CreateFont(device, fntContent, fontAtlas!, "Default Font");
    }
}

