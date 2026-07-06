using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable InconsistentNaming
namespace TestConsole;

public static partial class Wormhood
{
    public static void RenderTunnel_Pattern(
        RenderPass      pass,
        RenderConfig    config,
        Uniforms       	uniforms)
	{
        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(Wormhood_GPU_ShaderId, "Wormhood_encoder"u8);

        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(Wormhood_GPU_ShaderId, config, Wormhood_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref Wormhood_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (Wormhood_GPU_Cache)pipelineCache.bindGroupCache;
        
        pass_.SetBindGroupUniform(0, ref bindGroupCache.bindGroup0, uniforms, pipelineCache, "Wormhood_bindGroup0"u8);
        
        pass_.Draw(3, 1, 0, 0);
	}
    
    private sealed class Wormhood_GPU_Cache : BindGroupCache
    {
        internal            WgpuBindGroup                         bindGroup0;
        
        protected override void Clear() {
            ReleaseBindGroup(ref bindGroup0);
        }
    }
    
    private static readonly int Wormhood_GPU_ShaderId       =  ShaderRegistry.NewShaderId("WormhoodShader");
    private const  ulong        Wormhood_GPU_layout_0_key   =  0x1144;  // unique key set by Generator
    private static ulong        Wormhood_GPU_WgslHash       => 0x1244; // support Hot-Relead
    
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache Wormhood_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[1];
        var layout_0 = device.GetBindGroupLayout(Wormhood_GPU_layout_0_key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutUniform();
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Fragment, Wormhood_GPU_layout_0_key, "Wormhood_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        using var module = device.CreateShaderModule(Wormhood_GPU_Shader(), "Wormhood_shader"u8);

        var pipeline = device.CreateRenderPipeline(layouts, config, module, "vs_main"u8, module, "fs_main"u8, "Wormhood_pipeline"u8);
        
        var bindGroupCache = new Wormhood_GPU_Cache();
        return ref device.CreatePipelineCache(Wormhood_GPU_ShaderId, config, Wormhood_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static ReadOnlySpan<byte> Wormhood_GPU_Shader() => WgpuResource.GetResource(typeof(Wormhood), "shaders.raymarcher_no_texture.wgsl");
}