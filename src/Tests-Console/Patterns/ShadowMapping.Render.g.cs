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
    public static partial void Render(
        RenderPass      pass,
        RenderConfig    config,
        InBuffer<float> verticesBuffer,
        in Matrix4x4    modelViewProjectionMatrix)
	{
        var buffers =
        GpuBuffers.Create(verticesBuffer, nameof(verticesBuffer));
        
        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(Render_GPU_ShaderId, "Render_encoder"u8);
        
        recorder.RequireRead(verticesBuffer);

        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(Render_GPU_ShaderId, config, Render_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref Render_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (Render_GPU_Cache)pipelineCache.bindGroupCache;
        
        // --- bind group 0
        pass_.SetBindGroupUniform(0, ref bindGroupCache.bindGroup0, modelViewProjectionMatrix, pipelineCache, "Render_bindGroup0"u8);
        
        pass_.SetVertexBuffer(verticesBuffer, 0); // slot: 0 - [VertexBuffer(0)]  references:  desc.VertexState.buffers[0]
   
        // --- draw
        pass_.Draw(verticesBuffer, 0, config, 1, 0, 0);
	}
    
    private sealed class Render_GPU_Cache : BindGroupCache
    {
        internal WgpuBindGroup    bindGroup0 = new ();
        
        protected override void Clear() {
            ReleaseBindGroup(ref bindGroup0);
        }
    }
    
    private static readonly int Render_GPU_ShaderId            =  ShaderRegistry.NewShaderId("Render");
    private const  ulong        Render_GPU_layout_0_Key        =  0x4766;  // unique key set by Generator
    
    private static ulong        Render_GPU_WgslHash            => 0x1266;  // support Hot-Relead
    
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache Render_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[1];
        var layout_0 = device.GetBindGroupLayout(Render_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutUniform();
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, Render_GPU_layout_0_Key, "Render_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        using var vsModule = device.CreateShaderModule(Render_GPU_VertexShader(),   "Render_VertexShader"u8);
        using var fsModule = device.CreateShaderModule(Render_GPU_FragmentShader(), "Render_FragmentShader"u8);

        var pipeline = device.CreateRenderPipeline(layouts, config, vsModule, "main"u8, fsModule, "main"u8, "Render_pipeline"u8);

        var bindGroupCache = new Render_GPU_Cache();
        return ref device.CreatePipelineCache(Render_GPU_ShaderId, config, Render_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static ReadOnlySpan<byte> Render_GPU_VertexShader()   => WgpuResource.GetResource(typeof(TexturedCube), "shaders/basic.vert.wgsl");
    private static ReadOnlySpan<byte> Render_GPU_FragmentShader() => WgpuResource.GetResource(typeof(TexturedCube), "shaders/vertexPositionColor.frag.wgsl");
}