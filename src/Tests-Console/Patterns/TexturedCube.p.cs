using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable InconsistentNaming
namespace Shaders.TexturedCube;

public partial class Renderer
{
    private static void Pattern_RenderCube(
        RenderPass      pass,
        RenderConfig    config,
        in Uniforms   	uniforms,
        GpuSampler      smoothFilter,
        GpuTextureView  material,
        InBuffer<float> vertices)
	{
        var buffers =
        GpuBuffers.Create(vertices, nameof(vertices));
        
        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(TextureTest_GPU_ShaderId, "TextureTest_encoder"u8);
        
        recorder.RequireRead(vertices);

        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(TextureTest_GPU_ShaderId, config, TextureTest_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref TextureTest_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (TextureTest_GPU_Cache)pipelineCache.bindGroupCache;
        
        // --- bind group 0
        var key_0 = (smoothFilter.Handle, material.Handle);
        if (!bindGroupCache.bindGroup_0.TryGetValue(key_0, out var bindGroup0)) {
            recorder.BindGroupEntryUniform<Uniforms>(0);
            recorder.BindGroupEntrySampler(1, smoothFilter);
            recorder.BindGroupEntryTexture(2, material);
            bindGroup0 = recorder.CreateBindGroup(pipelineCache.layouts[0], "TextureTest_bindGroup_0"u8);
            bindGroupCache.bindGroup_0.Add(key_0, bindGroup0);
        }
        pass_.AddUniform(uniforms);
        pass_.SetBindGroupUniforms(0, bindGroup0);
        
        pass_.SetVertexBuffer(vertices, 0); // slot: 0 - [VertexBuffer(0)]  references:  desc.VertexState.buffers[0]
   
        // --- draw
        pass_.Draw(vertices, 0, config, new DrawArgs());
	}
    
    private sealed class TextureTest_GPU_Cache : BindGroupCache
    {
        internal readonly   Dictionary<(nint,nint), WgpuBindGroup>    bindGroup_0 = new ();
        
        protected override void Clear() {
            ReleaseBindGroups(bindGroup_0);
        }
    }
    
    private static readonly int TextureTest_GPU_ShaderId            =  ShaderRegistry.NewShaderId("TextureTest");
    private const  ulong        TextureTest_GPU_layout_0_Key        =  0x4755;  // unique key set by Generator
    
    private static ulong        TextureTest_GPU_WgslHash            => 0x1255;  // support Hot-Relead
    
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache TextureTest_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[1];
        var layout_0 = device.GetBindGroupLayout(TextureTest_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutUniform(0);
            device.BindGroupLayoutSampler(1, SamplerBindingType.Filtering);
            device.BindGroupLayoutTexture(2, TextureSampleType.Float, TextureViewDimension.D2D, false);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, TextureTest_GPU_layout_0_Key, "TextureTest_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(Renderer), TextureTest_GPU_Shaders, "TextureTest_pipeline"u8);

        var bindGroupCache = new TextureTest_GPU_Cache();
        return ref device.CreatePipelineCache(TextureTest_GPU_ShaderId, config, TextureTest_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] TextureTest_GPU_Shaders = [
        new("shaders/basic.vert.wgsl",                   vert: "main"),
        new("shaders/sampleTextureMixColor.frag.wgsl",   frag: "main")
    ];
}