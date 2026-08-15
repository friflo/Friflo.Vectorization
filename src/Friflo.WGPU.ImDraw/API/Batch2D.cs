// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Shaders.Imdraw;


// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
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


internal struct DrawCommand
{
    internal int                    zIndex;
    internal int                    sequence;
    internal GpuTextureView         texture;
    internal InOutView<Vertex2D>    vertexView;
    internal InView<uint>           indexView;
    internal RenderConfig           config;
    internal ImUniforms             uniforms;
    internal GpuSampler             sampler;
    internal RectVector2            scissor;

    public override string ToString() => $"zIndex: {zIndex}, {sequence}   quads: {indexView.Length / 4}   {texture}";
}

internal struct CmdSegment
{
    internal int    zIndex;
    internal int    sequence;
    internal int    index;
    internal int    length;

    public override string ToString() => $"zIndex: {zIndex}, {sequence}   [{index}, {length}]";
}

internal readonly struct RectVector2 (Vector2 pos, Vector2 size) : IEquatable<RectVector2> 
{
    internal readonly Vector2    pos     = pos;
    internal readonly Vector2    size    = size;

    public override string ToString()       => $"{pos} {size}";

    public bool Equals(RectVector2 other)   => pos == other.pos && size == other.size;
}



public sealed partial class Batch2D : IDisposable
{
    internal readonly   DrawModule          drawModule;
    internal readonly   RenderConfig[]      renderConfigs;              // each RenderConfig is a 4 bytes ID
    internal readonly   GpuBuffer<Vertex2D> vertexBuffer;
    internal readonly   GpuBuffer<uint>     indexBuffer;
    
    internal readonly   List<DrawCommand>   drawCommands 	= [];
    internal readonly   List<CmdSegment>    commandSegments = [];
    internal readonly   Stack<RectVector2>  scissorStack    = [];
    internal readonly   Stack<Matrix4x4>    transformStack  = [];
    internal readonly   Gui                 gui;
    public   readonly   GuiInput            input           = new();
    
    // --- resources owned by DrawModule
    internal readonly   GpuTextureView      defaultWhiteTextureView;
    internal readonly   GpuSampler          samplerLinear;              // the default sampler
    internal readonly   GpuSampler          samplerNearest;
    internal            Font?               defaultFont;
    
    // --- Draw2D - state
    internal            Vector2             viewport;
    internal            Matrix4x4           defaultOrtho;
    internal            Matrix4x4           currentTransform;
    internal            BlendState          currentBlendState;
    internal            GpuSampler          currentSampler;
    internal            RectVector2         currentScissor;
    internal            bool                sortZIndex;
    internal            int                 currentZIndex;
    internal            int                 currentSequence;
    internal            ImUniforms          uniforms;
    internal            int                 vertexStart;                // start of next Draw()
    internal            int                 vertexCount;
    internal            GpuTextureView?     currentTextureView;


    public void Dispose()
    {
        vertexBuffer.Dispose();
        indexBuffer.Dispose();
    }
    
    public Font GetDefaultFont() => defaultFont ??= drawModule.GetDefaultFont();

    
    /// <summary>
    /// Core constructor supporting a fully custom GpuSamplerDescriptor (or default Linear sampler if null).
    /// </summary>
    public Batch2D(
        GpuDevice               device,
        TextureFormat           targetFormat,
        int                     maxVertices         = 60_000)
    {
        gui = new Gui(input);
        
        if (!device.TryGetModule(out drawModule)) {
            drawModule = new DrawModule(device);
            device.AddModule(drawModule);
        }
        
        // --- vertex & index buffer - to draw quads
        int maxQuads   = maxVertices / 4;
        int maxIndices = maxQuads * 6;

        vertexBuffer = device.CreateBuffer<Vertex2D>(maxVertices, default, "Batch2D Vertices", BufferProfile.StaticIn, BufferType.Vertex);

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
        indexBuffer = device.CreateBuffer(indices, "Batch2D Indices", BufferProfile.StaticIn, BufferType.Index);
        indexBuffer.In().Write();
        
        defaultWhiteTextureView = drawModule.defaultWhiteTextureView;
        samplerLinear           = drawModule.samplerLinear;
        samplerNearest          = drawModule.samplerNearest;
        currentSampler          = samplerLinear;

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
    
    public DrawGui BeginGui(in RenderFrame frame, in GpuRenderPassDescriptor descriptor)
    {
        var draw = BeginDraw2D(frame, descriptor);
        return draw.BeginGui();
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
        currentTransform    = Matrix4x4.Identity;
        currentBlendState   = BlendState.Alpha;
        currentScissor      = new RectVector2 ( Vector2.Zero, new Vector2(frame.Width, frame.Height));
        sortZIndex          = false;
        currentZIndex       = 0;
        currentSequence     = 0;

        scissorStack.Clear();
        transformStack.Clear();
        drawCommands.Clear();
        
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
                    [IndexBuffer]   [Draw]  InBuffer<uint>      indices);

    
    
    public void AddEvent(in ImEvent ev)
    {
        input.AddEvent(ev);
    }
}

