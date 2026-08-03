using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable InconsistentNaming
namespace Shaders.RenderTest;

public partial class Renderer
{
    private static partial void DeformVertices(
        PipelineContext         context,
        InOutBuffer<VertexData> vertices,
        float                   time)
	{
        return;
		var recorder	= (CommandRecorder)context;
		recorder.InitKernel(DeformVertices_GPU_ShaderId, "DeformVertices_pipeline"u8);
        
        recorder.RequireReadWrite(vertices);
        
        using var pass_ = recorder.BeginComputePass("DeformVertices"u8);

        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(DeformVertices_GPU_ShaderId, DeformVertices_GPU_WgslHash); 
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref DeformVertices_GPU_CreatePipelineCache(recorder.Device);
        }
        pass_.SetPipeline(pipelineCache.computePipeline);
        
        var bindGroupCache = (DeformVertices_GPU_Cache)pipelineCache.bindGroupCache;
        
        // --- bind group 0
        var key_0 = vertices.Handle;
        if (!bindGroupCache.bindGroup_0.TryGetValue(key_0, out var bindGroup0)) {
            recorder.BindGroupEntryBuffer(0, vertices.Buffer);
            bindGroup0 = recorder.CreateBindGroup(pipelineCache.bufferLayout, "DeformVertices_bindGroup_0"u8);
            bindGroupCache.bindGroup_0.Add(key_0, bindGroup0);
        }
        pass_.SetBindGroup(0, bindGroup0);
        
        // --- bind group 1
        pass_.SetBindGroupUniform(1, 0, ref bindGroupCache.bindGroup_1, time, pipelineCache, "DeformVertices_bindGroup_1"u8);
        
        // --- compute
        pass_.DispatchWorkgroups((vertices.Length + 63) / 64, 1, 1);
	}
    
    private sealed class DeformVertices_GPU_Cache : BindGroupCache
    {
        internal readonly   Dictionary<nint,    WgpuBindGroup>    bindGroup_0 = new ();
        internal            WgpuBindGroup                         bindGroup_1;
        
        protected override void Clear() {
            ReleaseBindGroups(bindGroup_0);
            ReleaseBindGroup(ref bindGroup_1);
        }
    }
    
    private static readonly int DeformVertices_GPU_ShaderId      =  ShaderRegistry.NewShaderId("Triangles");
    private const  ulong        DeformVertices_GPU_layout_0_key  =  0x11_47;  // unique key set by Generator
    private const  ulong        DeformVertices_GPU_layout_1_key  =  0x11_11;  // unique key set by Generator
    
    private static ulong        DeformVertices_GPU_WgslHash      => 0x11_123; // support Hot-Relead
    
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly ComputeCache DeformVertices_GPU_CreatePipelineCache(WgpuDevice device)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[2];
        var layout_0 = device.GetBindGroupLayout(DeformVertices_GPU_layout_0_key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutBuffer(0, BufferBindingType.Storage);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Compute, DeformVertices_GPU_layout_0_key, "DeformVertices_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        var layout_1 = device.GetBindGroupLayout(DeformVertices_GPU_layout_1_key);
        if (!layout_1.IsCreated) {
            device.BindGroupLayoutUniform(0);
            layout_1 = device.CreateBindGroupLayout(ShaderStage.Compute, DeformVertices_GPU_layout_1_key, "DeformVertices_layout_1"u8);
        }
        layouts[1] = layout_1;
        
        // var pipeline = device.CreateRenderPipeline(layouts, config, typeof(Renderer), DeformVertices_GPU_Shaders, "DeformVertices_pipeline"u8);
        
        using var shaderModule  = device.CreateShaderModule(DeformVertices_GPU_Shader(), "DeformVertices"u8);
        var pipeline = device.CreateComputePipeline(shaderModule, layout_0, layout_1, "cs_main"u8);
        
        var bindGroupCache = new DeformVertices_GPU_Cache();
        return ref device.CreatePipelineCache(DeformVertices_GPU_ShaderId, DeformVertices_GPU_WgslHash, pipeline, layout_0, layout_1, bindGroupCache);
    }
    
    private static readonly WgpuShader[] DeformVertices_GPU_Shaders = [
        new("shaders/deform.wgsl")
    ];
    
    private static ReadOnlySpan<byte>DeformVertices_GPU_Shader() =>
"""
struct VertexData {
    position: vec4<f32>,
    color: vec4<f32>,
}

@group(0) @binding(0) var<storage, read_write>  vertices:   array<VertexData>;
@group(1) @binding(0) var<storage, read>        time:       f32;

@compute @workgroup_size(64)
fn cs_main(@builtin(global_invocation_id) global_id: vec3<u32>) {
    let index = global_id.x;
    
    // Safety check: Nicht über das Array hinaus schreiben
    if (index >= arrayLength(&vertices)) {
        return;
    }

    // Beispiel-Manipulation: Schwingung der Y-Position basierend auf X & Zeit
    let base_x = vertices[index].position.x;
    vertices[index].position.y += sin(time * 3.0 + base_x * 4.0) * 0.005;

    // Optional: Ändere sanft die Alpha- oder Farbwerte
    vertices[index].color.r = 0.5 + 0.5 * sin(time + base_x);
}
"""u8;
    
}