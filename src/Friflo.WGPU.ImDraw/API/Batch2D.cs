// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Friflo.GPU;
using Shaders.Imdraw;

// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public enum BlendState
{
    /** Standard transparent (default) */               Alpha,  
    /** Overwrites pixels completely (no blending) */   Opaque,
    /** Glow, light, particles (SrcAlpha + One) */      Additive,
    /** Shadows, tinting (Zero + Src) */                Multiply,
    /** Add colors directly */                          AddColors,
    /** Subtract colors directly */                     SubtractColors
}

public enum SamplerFilter
{
    /**  Hard/Pixelated edges (Pixel Art). */                   Nearest,
    /**  Smooth/Blended edges (Scales, High-Res Sprites). */    Linear
}


public sealed partial class Batch2D : IDisposable
{
#region internal
    private  readonly   DrawModule          drawModule;
    internal readonly   RenderConfig[]      renderConfigs;              // each RenderConfig is a 4 bytes ID
    internal readonly   Memory<Vertex2D>    vertexBuffer;
    internal readonly   Memory<uint>        indexBuffer;
    internal readonly   GpuBuffer<Vertex2D> gpuVertexBuffer;
    internal readonly   GpuBuffer<uint>     gpuIndexBuffer;
    
    internal readonly   List<DrawCommand>   drawCommands 	    = [];
    internal readonly   List<CmdSegment>    commandSegments     = [];
    internal readonly   Stack<RectVector2>  scissorStack        = [];
    internal readonly   Stack<Matrix4x4>    transformStack      = [];
    internal readonly   Stack<int>          zIndexStack         = [];
    internal readonly   Stack<SamplerFilter>samplerFilterStack  = [];
    private  readonly   StringBuilder       stringBuilder       = new(512,512); // => first chunk: 512 chars
    internal readonly   GuiState            guiState            = new();

    // --- resources owned by DrawModule
    internal readonly   GuiHost             host;
    public   readonly   GuiInput            input;
    internal readonly   GpuSampler          samplerLinear;              // the default sampler
    internal readonly   GpuSampler          samplerNearest;
    internal            Font                defaultFont;
    internal            ImTexture           defaultFontTexture;
    
    // --- Draw2D - state
    internal            IFormatProvider     formatProvider;
    internal            Vector2             viewport;
    internal            Matrix4x4           defaultOrtho;
    internal            Matrix4x4           currentTransform;
    internal            BlendState          currentBlendState;
    internal            SamplerFilter       currentSamplerFilter;
    internal            RectVector2         currentScissor;
    internal            bool                sortZIndex;
    internal            int                 currentZIndex;
    internal            int                 currentSequence;
    internal            ImUniforms          uniforms;
    internal            int                 vertexStart;                // start of next Draw()
    internal            int                 vertexCount;
    internal            ImTexture           currentTexture;

    
    internal Batch2D(GpuDevice device, TextureFormat targetFormat, int maxVertices)
    {
        if (!device.TryGetModule(out drawModule)) {
            drawModule = new DrawModule(device);
            device.AddModule(drawModule);
        }
        formatProvider  = CultureInfo.InvariantCulture;
        host            = drawModule.guiModule.host;
        input           = drawModule.guiModule.input;
        
        // --- vertex & index buffer - to draw quads
        int maxQuads   = maxVertices / 4;
        int maxIndices = maxQuads * 6;

        vertexBuffer = new Memory<Vertex2D>(new Vertex2D[maxVertices]);
        gpuVertexBuffer = device.CreateBuffer(vertexBuffer, "Batch2D Vertices", BufferProfile.StaticIn, BufferType.Vertex);

        // generate quad indexes only once
        var indices = new uint[maxIndices];
        for (int i = 0, v = 0; i < maxIndices; i += 6, v += 4)
        {
            indices[i + 0] = (uint)(v + 0);
            indices[i + 1] = (uint)(v + 1);
            indices[i + 2] = (uint)(v + 2);
            indices[i + 3] = (uint)(v + 2);
            indices[i + 4] = (uint)(v + 3);
            indices[i + 5] = (uint)(v + 0);
        }
        indexBuffer = new  Memory<uint>(indices);
        gpuIndexBuffer = device.CreateBuffer(indices, "Batch2D Indices", BufferProfile.StaticIn, BufferType.Index);
        gpuIndexBuffer.In().Write();
        
        defaultFont             = drawModule.defaultFont;
        defaultFontTexture      = drawModule.defaultFont.texture;
        samplerLinear           = drawModule.samplerLinear;
        samplerNearest          = drawModule.samplerNearest;
        currentSamplerFilter    = SamplerFilter.Linear;

        renderConfigs = CreateRenderConfigs(targetFormat);
    }
    
    // TextureFormat.BGRA8Unorm / RGBA8Unorm
    private static RenderConfig[] CreateRenderConfigs(TextureFormat targetFormat)
    {
        var configs = new RenderConfig[6];
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
        desc.PrimitiveState = new GpuPrimitiveState {
            topology    = PrimitiveTopology.TriangleList,
            cullMode    = CullMode.None
        };
        for (int index = 0; index < configs.Length; index++) {
            var blendIndex  = (BlendState)index;
            var blend       = CreateBlendState(blendIndex);
            desc.FragmentState = new GpuFragmentState{ targets = [ new GpuColorTargetState { format = targetFormat, blend  = blend }]};
            configs[index] = desc.CreateConfig($"Batch2D: {blendIndex}"); // does not create any wgpu handle
        }
        return configs;
    }
    
    private static GpuBlendState CreateBlendState(BlendState blendIndex)
    {
        switch (blendIndex)
        {
            case BlendState.Alpha: return new GpuBlendState {
                color = new GpuBlendComponent { srcFactor = BlendFactor.SrcAlpha,   dstFactor = BlendFactor.OneMinusSrcAlpha, operation = BlendOperation.Add },
                alpha = new GpuBlendComponent { srcFactor = BlendFactor.One,        dstFactor = BlendFactor.OneMinusSrcAlpha, operation = BlendOperation.Add }
            };
            case BlendState.Opaque: return new GpuBlendState {
                color = new GpuBlendComponent { srcFactor = BlendFactor.One,        dstFactor = BlendFactor.Zero, operation = BlendOperation.Add },
                alpha = new GpuBlendComponent { srcFactor = BlendFactor.One,        dstFactor = BlendFactor.Zero, operation = BlendOperation.Add }
            };
            case BlendState.Additive: return new GpuBlendState {
                color = new GpuBlendComponent { srcFactor = BlendFactor.SrcAlpha,   dstFactor = BlendFactor.One, operation = BlendOperation.Add },
                alpha = new GpuBlendComponent { srcFactor = BlendFactor.Zero,       dstFactor = BlendFactor.One, operation = BlendOperation.Add }
            };
            case BlendState.Multiply: return new GpuBlendState {
                color = new GpuBlendComponent { srcFactor = BlendFactor.Zero,       dstFactor = BlendFactor.Src, operation = BlendOperation.Add },
                alpha = new GpuBlendComponent { srcFactor = BlendFactor.Zero,       dstFactor = BlendFactor.One, operation = BlendOperation.Add }
            };
            case BlendState.AddColors: return new GpuBlendState {
                color = new GpuBlendComponent { srcFactor = BlendFactor.Src,        dstFactor = BlendFactor.One, operation = BlendOperation.Add },
                alpha = new GpuBlendComponent { srcFactor = BlendFactor.Zero,       dstFactor = BlendFactor.One, operation = BlendOperation.Add }
            };
            case BlendState.SubtractColors: return new GpuBlendState {
                color = new GpuBlendComponent { srcFactor = BlendFactor.One,        dstFactor = BlendFactor.One, operation = BlendOperation.ReverseSubtract },
                alpha = new GpuBlendComponent { srcFactor = BlendFactor.Zero,       dstFactor = BlendFactor.One, operation = BlendOperation.Add }
            };
        }
        throw new ArgumentOutOfRangeException(nameof(blendIndex));
    }
    
    internal StringBuilder StringBuilder()
    {
        stringBuilder.Clear();
        return stringBuilder;
    }
    
    [Shader("~/shaders/imdraw/draw2d.wgsl", vertex: "vs_main", fragment: "fs_main")]
    internal static partial void Draw(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]               in ImUniforms       globals,
        [Map(0, 1)] [texture_2d(ST.f32)]    GpuTextureView      texture,
        [Map(0, 2)] [sampler]               GpuSampler          sampler,
                    [VertexBuffer(0)]       InBuffer<Vertex2D>  vertices,
                    [IndexBuffer]   [Draw]  InBuffer<uint>      indices);
#endregion


#region public
    public void Dispose()
    {
        gpuVertexBuffer.Dispose();
        gpuIndexBuffer.Dispose();
    }
    
    public void AddEvent(in ImEvent ev) => input.AddEvent(ev);
    
    public void SetFont(Font font) {
        defaultFont         = font;
        defaultFontTexture  = font.texture;
    }
    
    public void SetFontDefault() => SetFont(drawModule.defaultFont);
    
    public void SetFormatProvider(IFormatProvider provider) => formatProvider = provider;
    
    public Gui BeginGui(int width, int height)
    {
        var draw = BeginDraw2D(width, height);
        return draw.BeginGui();
    }
    
    public Draw2D BeginDraw2D(int width, int height)
    {
        // reset batcher state
        /* if (defaultFontTexture.IsDisposed) {    // TODO IM_TEX
            SetFontDefault();
        } */
        guiState.Reset();
        currentTexture      = defaultFontTexture;
        vertexStart         = 0;
        vertexCount         = 0;
        currentSamplerFilter= SamplerFilter.Linear;
        currentTransform    = Matrix4x4.Identity;
        currentBlendState   = BlendState.Alpha;
        currentScissor      = new RectVector2(Vector2.Zero, new Vector2(width, height));
        sortZIndex          = false;
        currentZIndex       = 0;
        currentSequence     = 0;

        drawCommands.Clear();
        scissorStack.Clear();
        transformStack.Clear();
        zIndexStack.Clear();
        samplerFilterStack.Clear();
        
        var draw = new Draw2D(this);
        draw.SetViewport(width, height);
        return draw;
    }
#endregion
}

