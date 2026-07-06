using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable InconsistentNaming
namespace TestConsole;

public partial class TexturedCube
{
    public static void RenderCube_Pattern(
        RenderPass      pass,
        RenderConfig    config,
        InBuffer<float> vertices,
        Uniforms     	uniforms,
        GpuSampler      smoothFilter,
        GpuTextureView  material)
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
        if (!bindGroupCache.bindGroup0.TryGetValue(key_0, out var bindGroup0)) {
            recorder.BindGroupEntryUniform<Uniforms>();
            recorder.BindGroupEntrySampler(smoothFilter);
            recorder.BindGroupEntryTexture(material);
            bindGroup0 = recorder.CreateBindGroup(pipelineCache.layouts[0], "TextureTest_bindGroup0"u8);
            bindGroupCache.bindGroup0.Add(key_0, bindGroup0);
        }
        pass_.AddUniform(uniforms);
        pass_.SetBindGroupUniforms(0, bindGroup0);
        
        pass_.SetVertexBuffer(vertices, 0); // slot: 0 - [VertexBuffer(0)]  references:  desc.VertexState.buffers[0]
   
        // --- draw
        pass_.Draw(vertices, 0, config, 1, 0, 0);
	}
    
    private sealed class TextureTest_GPU_Cache : BindGroupCache
    {
        internal readonly   Dictionary<(nint,nint), WgpuBindGroup>    bindGroup0 = new ();
        
        protected override void Clear() {
            ReleaseBindGroups(bindGroup0);
        }
    }
    
    private static readonly int TextureTest_GPU_ShaderId            =  ShaderRegistry.NewShaderId("TextureTestShader");
    private const  ulong        TextureTest_GPU_layout_0_Key        =  0x4755;  // unique key set by Generator
    
    private static ulong        TextureTest_GPU_WgslHash            => 0x1255;  // support Hot-Relead
    
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache TextureTest_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[1];
        var layout_0 = device.GetBindGroupLayout(TextureTest_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutUniform();
            device.BindGroupLayoutSampler(SamplerBindingType.Filtering);
            device.BindGroupLayoutTexture(TextureSampleType.Float, TextureViewDimension.D2D, false);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, TextureTest_GPU_layout_0_Key, "TextureTest_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        using var vsModule = device.CreateShaderModule(TextureTest_GPU_VertexShader(),   "TextureTest_VertexShader"u8);
        using var fsModule = device.CreateShaderModule(TextureTest_GPU_FragmentShader(), "TextureTest_FragmentShader"u8);

        var pipeline = device.CreateRenderPipeline(layouts, config, vsModule, "main"u8, fsModule, "main"u8, "TextureTest_pipeline"u8);

        var bindGroupCache = new TextureTest_GPU_Cache();
        return ref device.CreatePipelineCache(TextureTest_GPU_ShaderId, config, TextureTest_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static ReadOnlySpan<byte> TextureTest_GPU_VertexShader()   => WgpuResource.GetResource(typeof(TexturedCube), "shaders/basic.vert.wgsl");
    private static ReadOnlySpan<byte> TextureTest_GPU_FragmentShader() => WgpuResource.GetResource(typeof(TexturedCube), "shaders/sampleTextureMixColor.frag.wgsl");
}