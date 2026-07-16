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
    private static void Pattern_Shadow(
        RenderPass          pass,
        RenderConfig        config,
        in Scene            scene,
        in Model            model,
        InBuffer<Vector3>   verticesBuffer,
        InBuffer<ushort>    indexBuffer)
	{
        var buffers =
        GpuBuffers.Create(verticesBuffer, nameof(verticesBuffer));
        
        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(Shadow_GPU_ShaderId, "Shadow_encoder"u8);
        
        recorder.RequireRead(verticesBuffer);
        recorder.RequireRead(indexBuffer);

        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(Shadow_GPU_ShaderId, config, Shadow_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref Shadow_GPU_CreatePipelineCache(recorder.Device, config);
        }

        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (Shadow_GPU_Cache)pipelineCache.bindGroupCache;
        
        // --- bind group 0
        pass_.SetBindGroupUniform(0, 0, ref bindGroupCache.bindGroup_0, scene, pipelineCache, "Shadow_bindGroup_0"u8);
        
        // --- bind group 1
        pass_.SetBindGroupUniform(1, 0, ref bindGroupCache.bindGroup_1, model, pipelineCache, "Shadow_bindGroup_1"u8);
        
        pass_.SetVertexBuffer(verticesBuffer, 0);
        
        pass_.SetIndexBuffer(indexBuffer, IndexFormat.Uint16);
   
        // --- draw
        pass_.DrawIndexed(indexBuffer, new DrawArgs(0, 1, 0, 0));
	}
    
    private sealed class Shadow_GPU_Cache : BindGroupCache
    {
        internal WgpuBindGroup    bindGroup_0;
        internal WgpuBindGroup    bindGroup_1;
        
        protected override void Clear() {
            ReleaseBindGroup(ref bindGroup_0);
            ReleaseBindGroup(ref bindGroup_1);
        }
    }
    
    private static readonly int Shadow_GPU_ShaderId            =  ShaderRegistry.NewShaderId("Shadow");
    private const  ulong        Shadow_GPU_layout_0_Key        =  0x1000;
    private const  ulong        Shadow_GPU_layout_1_Key        =  0x1001;
    
    private static ulong        Shadow_GPU_WgslHash            => 0x1266;  // support Hot-Relead
    
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache Shadow_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[2];
        var layout_0 = device.GetBindGroupLayout(Shadow_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutUniform(0);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex, Shadow_GPU_layout_0_Key, "Shadow_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        var layout_1 = device.GetBindGroupLayout(Shadow_GPU_layout_1_Key);
        if (!layout_1.IsCreated) {
            device.BindGroupLayoutUniform(0);
            layout_1 = device.CreateBindGroupLayout(ShaderStage.Vertex, Shadow_GPU_layout_1_Key, "Shadow_layout_1"u8);
        }
        layouts[1] = layout_1;
        
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(ShadowMapping), Shadow_GPU_Shaders, "Shadow_pipeline"u8);

        var bindGroupCache = new Shadow_GPU_Cache();
        return ref device.CreatePipelineCache(Shadow_GPU_ShaderId, config, Shadow_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] Shadow_GPU_Shaders = [
        new("shaders/shadowMapping/vertexShadow.wgsl", vert: "main")
    ];
}