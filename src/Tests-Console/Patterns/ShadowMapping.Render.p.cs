using System.Numerics;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable InconsistentNaming
namespace Shaders.ShadowMapping;

public partial class Renderer
{
    private static void Pattern_RenderScene(
        RenderPass          pass,
        RenderConfig        config,
        in Scene            scene,
        GpuTextureView      textureView,
        GpuSampler          sampler,
        in Model            model,
        InBuffer<Vector3>   verticesBuffer,
        InBuffer<ushort>    indexBuffer)
	{
        var buffers =
        GpuBuffers.Create(verticesBuffer, nameof(verticesBuffer));
        
        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(Render_GPU_ShaderId, "Render_encoder"u8);
        
        recorder.RequireRead(verticesBuffer);
        recorder.RequireRead(indexBuffer);

        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(Render_GPU_ShaderId, config, Render_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref Render_GPU_CreatePipelineCache(recorder.Device, config);
        }

        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (Render_GPU_Cache)pipelineCache.bindGroupCache;
        
        // --- bind group 0
        var key_0 = (textureView.Handle, sampler.Handle);
        if (!bindGroupCache.bindGroup_0.TryGetValue(key_0, out var bindGroup0)) {
            recorder.BindGroupEntryUniform<Scene>(0);
            recorder.BindGroupEntryTexture(1, textureView);
            recorder.BindGroupEntrySampler(2, sampler);
            bindGroup0 = recorder.CreateBindGroup(pipelineCache.layouts[0], "TextureTest_bindGroup_0"u8);
            bindGroupCache.bindGroup_0.Add(key_0, bindGroup0);
        }
        pass_.AddUniform(scene);
        pass_.SetBindGroupUniforms(0, bindGroup0);
        
        // --- bind group 1
        pass_.SetBindGroupUniform(1, 0, ref bindGroupCache.bindGroup1, model, pipelineCache, "Render_bindGroup_1"u8);
        
        pass_.SetVertexBuffer(verticesBuffer, 0);
        
        pass_.SetIndexBuffer(indexBuffer, IndexFormat.Uint16);
        
        // --- draw
        pass_.DrawIndexed(indexBuffer, new DrawArgs(0, 1, 0, 0));
	}
    
    private sealed class Render_GPU_Cache : BindGroupCache
    {
        internal readonly   Dictionary<(nint,nint), WgpuBindGroup>    bindGroup_0 = new ();
        internal WgpuBindGroup    bindGroup1;
        
        protected override void Clear() {
            ReleaseBindGroups(bindGroup_0);
            ReleaseBindGroup(ref bindGroup1);
        }
    }
    
    private static readonly int Render_GPU_ShaderId            =  ShaderRegistry.NewShaderId("Render");
    private const  ulong        Render_GPU_layout_0_Key        =  0x2000;
    private const  ulong        Render_GPU_layout_1_Key        =  0x2001;
    
    private static ulong        Render_GPU_WgslHash            => 0x1266;  // support Hot-Relead
    
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache Render_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[2];
        var layout_0 = device.GetBindGroupLayout(Render_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutUniform(0);
            device.BindGroupLayoutTexture(1, TextureSampleType.Depth, TextureViewDimension.D2D, false);
            device.BindGroupLayoutSampler(2, SamplerBindingType.Comparison);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, Render_GPU_layout_0_Key, "Render_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        var layout_1 = device.GetBindGroupLayout(Render_GPU_layout_1_Key);
        if (!layout_1.IsCreated) {
            device.BindGroupLayoutUniform(0);
            layout_1 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, Render_GPU_layout_1_Key, "Render_layout_1"u8);
        }
        layouts[1] = layout_1;
        
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(Renderer), Render_GPU_Shaders, "Render_pipeline"u8);

        var bindGroupCache = new Render_GPU_Cache();
        return ref device.CreatePipelineCache(Render_GPU_ShaderId, config, Render_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] Render_GPU_Shaders = [
        new("shaders/shadowMapping/vertex.wgsl",   vert: "main"),
        new("shaders/shadowMapping/fragment.wgsl", frag: "main"),
    ];
}