using System.Numerics;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable InconsistentNaming
namespace TestConsole;

public partial class ShadowMapping
{
    public static partial void Shadow(
        RenderPass      pass,
        RenderConfig    config,
        InBuffer<float> verticesBuffer,
        in Matrix4x4    modelViewProjectionMatrix)
	{
        var buffers =
        GpuBuffers.Create(verticesBuffer, nameof(verticesBuffer));
        
        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(Shadow_GPU_ShaderId, "Shadow_encoder"u8);
        
        recorder.RequireRead(verticesBuffer);

        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(Shadow_GPU_ShaderId, config, Shadow_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref Shadow_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (Shadow_GPU_Cache)pipelineCache.bindGroupCache;
        
        // --- bind group 0
        pass_.SetBindGroupUniform(0, ref bindGroupCache.bindGroup0, modelViewProjectionMatrix, pipelineCache, "Shadow_bindGroup0"u8);
        
        pass_.SetVertexBuffer(verticesBuffer, 0); // slot: 0 - [VertexBuffer(0)]  references:  desc.VertexState.buffers[0]
   
        // --- draw
        pass_.Draw(verticesBuffer, 0, config, 1, 0, 0);
	}
    
    private sealed class Shadow_GPU_Cache : BindGroupCache
    {
        internal WgpuBindGroup    bindGroup0 = new ();
        
        protected override void Clear() {
            ReleaseBindGroup(ref bindGroup0);
        }
    }
    
    private static readonly int Shadow_GPU_ShaderId            =  ShaderRegistry.NewShaderId("Shadow");
    private const  ulong        Shadow_GPU_layout_0_Key        =  0x4766;  // unique key set by Generator
    
    private static ulong        Shadow_GPU_WgslHash            => 0x1266;  // support Hot-Relead
    
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache Shadow_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[1];
        var layout_0 = device.GetBindGroupLayout(Shadow_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutUniform();
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, Shadow_GPU_layout_0_Key, "Shadow_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        using var vsModule = device.CreateShaderModule(Shadow_GPU_VertexShader(),   "Shadow_VertexShader"u8);
        using var fsModule = device.CreateShaderModule(Shadow_GPU_FragmentShader(), "Shadow_FragmentShader"u8);

        var pipeline = device.CreateRenderPipeline(layouts, config, vsModule, "main"u8, fsModule, "main"u8, "Shadow_pipeline"u8);

        var bindGroupCache = new Shadow_GPU_Cache();
        return ref device.CreatePipelineCache(Shadow_GPU_ShaderId, config, Shadow_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static ReadOnlySpan<byte> Shadow_GPU_VertexShader()   => WgpuResource.GetResource(typeof(TexturedCube), "shaders/basic.vert.wgsl");
    private static ReadOnlySpan<byte> Shadow_GPU_FragmentShader() => WgpuResource.GetResource(typeof(TexturedCube), "shaders/vertexPositionColor.frag.wgsl");
}