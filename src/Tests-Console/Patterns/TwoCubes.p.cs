using System.Numerics;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable InconsistentNaming
namespace TestConsole;

public partial class TwoCubes
{
    private static void Pattern_RenderCube(
        RenderPass      pass,
        RenderConfig    config,
        in Matrix4x4    modelViewProjectionMatrix,
        InBuffer<float> verticesBuffer)
	{
        var buffers =
        GpuBuffers.Create(verticesBuffer, nameof(verticesBuffer));
        
        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(TextureTest_GPU_ShaderId, "TextureTest_encoder"u8);
        
        recorder.RequireRead(verticesBuffer);

        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(TextureTest_GPU_ShaderId, config, TextureTest_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref TextureTest_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (TextureTest_GPU_Cache)pipelineCache.bindGroupCache;
        
        // --- bind group 0
        pass_.SetBindGroupUniform(0, 0, ref bindGroupCache.bindGroup_0, modelViewProjectionMatrix, pipelineCache, "TextureTest_bindGroup_0"u8);
        
        pass_.SetVertexBuffer(verticesBuffer, 0); // slot: 0 - [VertexBuffer(0)]  references:  desc.VertexState.buffers[0]
   
        // --- draw
        pass_.Draw(verticesBuffer, 0, config, new DrawArgs());
	}
    
    private sealed class TextureTest_GPU_Cache : BindGroupCache
    {
        internal WgpuBindGroup    bindGroup_0;
        
        protected override void Clear() {
            ReleaseBindGroup(ref bindGroup_0);
        }
    }
    
    private static readonly int TextureTest_GPU_ShaderId            =  ShaderRegistry.NewShaderId("TextureTest");
    private const  ulong        TextureTest_GPU_layout_0_Key        =  0x4766;  // unique key set by Generator
    
    private static ulong        TextureTest_GPU_WgslHash            => 0x1266;  // support Hot-Relead
    
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache TextureTest_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[1];
        var layout_0 = device.GetBindGroupLayout(TextureTest_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutUniform(0);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, TextureTest_GPU_layout_0_Key, "TextureTest_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(TwoCubes), TextureTest_GPU_Shaders, "TextureTest_pipeline"u8);

        var bindGroupCache = new TextureTest_GPU_Cache();
        return ref device.CreatePipelineCache(TextureTest_GPU_ShaderId, config, TextureTest_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] TextureTest_GPU_Shaders = [
        new("shaders/basic.vert.wgsl",               vert: "main"),
        new("shaders/vertexPositionColor.frag.wgsl", frag: "main")
    ];
}