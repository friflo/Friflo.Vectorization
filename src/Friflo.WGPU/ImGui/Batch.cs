// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using Friflo.GPU;
using Friflo.ImGui;
using Shaders.Imdraw;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.WGPU.ImGui;

public sealed partial class WgpuBatch : ImBatch
{
    private  readonly   GpuSampler      samplerLinear;      // the default sampler
    private  readonly   GpuSampler      samplerNearest; 
    private  readonly   RenderConfig[]  renderConfigs;      // each RenderConfig is a 4 bytes ID. used to support blend states

    internal WgpuBatch(WgpuGuiBackend backend, TextureFormat targetFormat, int maxVertices)
        : base(backend, maxVertices)
    {
        renderConfigs   = CreateRenderConfigs(targetFormat);
        
        samplerLinear   = backend.samplerLinear;
        samplerNearest  = backend.samplerNearest;
    }
    
    
    // TextureFormat.BGRA8Unorm / RGBA8Unorm
    /// <summary> Create multiple configs. A single would be sufficient, if not supporting <see cref="GpuBlendState"/>. </summary>
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
            var blend       = GuiEffect.CreateBlendState(blendIndex);
            desc.FragmentState = new GpuFragmentState{ targets = [ new GpuColorTargetState { format = targetFormat, blend  = blend }]};
            configs[index] = desc.CreateConfig($"Batch2D: {blendIndex}"); // does not create any wgpu handle
        }
        return configs;
    }
    
    public void DrawCommandList(in RenderTarget target, in GpuRenderPassDescriptor descriptor)
    {
        EndBatch();
        
        var scissor = new RectVector2(Vector2.Zero, viewport);

        var vertices = ((ImWgpuBuffer<Vertex2D>)gpuVertexBuffer).native;
        var indices  = ((ImWgpuBuffer<uint>)    gpuIndexBuffer).native;

        descriptor.colorAttachments[0].view = target.View;
        using var pass = target.BeginRenderPass(descriptor);
        
        foreach (var cmd in DrawList)
        {
            if (!cmd.scissor.Equals(scissor)) {
                scissor = cmd.scissor;
                pass.SetScissorRect((int)scissor.pos.X, (int)scissor.pos.Y, (int)scissor.size.X, (int)scissor.size.Y);    
            }
            var texture     = new GpuTextureView((GpuTexture)cmd.texture.native!, cmd.texture.handle);
            var vertexView  = vertices.In(cmd.vertexView.offset, cmd.vertexView.length);
            var indexView   = indices. In(cmd.indexView.offset,  cmd.indexView.length);
            var sampler     = cmd.samplerFilter == SamplerFilter.Linear ? samplerLinear : samplerNearest;
            var uniforms    = new ImUniforms(cmd.projection);
            var config      = renderConfigs[(int)cmd.blendState];
            
            Draw(pass, config, uniforms, texture, sampler, vertexView, indexView);
        }
    }
    
    [NoEmit]
    [Shader("~/shaders/imdraw/draw2d.wgsl", vertex: "vs_main", fragment: "fs_main")]
    private static partial void Draw(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]               in ImUniforms       globals,
        [Map(0, 1)] [texture_2d(ST.f32)]    GpuTextureView      texture,
        [Map(0, 2)] [sampler]               GpuSampler          sampler,
                    [VertexBuffer(0)]       InBuffer<Vertex2D>  vertices,
                    [IndexBuffer]   [Draw]  InBuffer<uint>      indices);
}