using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable InconsistentNaming
namespace TestConsole;

public partial class RenderTest
{
    public static void Pattern_DrawTriangles(
        RenderPass              pass,
        RenderConfig            config,
        InBuffer<VertexData>    triangles,
        in MyUniform            myUniform)
	{
        var buffers =
        GpuBuffers.Create(triangles, nameof(triangles));
        
        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(Triangles_GPU_ShaderId, "Triangles_encoder"u8);
        
        recorder.RequireRead(triangles);

        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(Triangles_GPU_ShaderId, config, Triangles_GPU_WgslHash); 
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref Triangles_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (Triangles_GPU_Cache)pipelineCache.bindGroupCache;
        
        // --- bind group 0
        var key_0 = triangles.Handle;
        if (!bindGroupCache.bindGroup0.TryGetValue(key_0, out var bindGroup0)) {
            recorder.BindGroupEntryBuffer(0, triangles.Buffer);
            bindGroup0 = recorder.CreateBindGroup(pipelineCache.layouts[0], "Triangles_bindGroup0"u8);
            bindGroupCache.bindGroup0.Add(key_0, bindGroup0);
        }
        pass_.SetBindGroup(0, bindGroup0);
        
        // --- bind group 2
        pass_.SetBindGroupUniform(2, 0, ref bindGroupCache.bindGroup2, myUniform, pipelineCache,"Triangles_bindGroup2"u8);
        
        // --- draw
        pass_.Draw(triangles, new DrawArgs());
	}
    
    private sealed class Triangles_GPU_Cache : BindGroupCache
    {
        internal readonly   Dictionary<nint,    WgpuBindGroup>    bindGroup0 = new ();
        internal            WgpuBindGroup                         bindGroup2;
        
        protected override void Clear() {
            ReleaseBindGroups(bindGroup0);
            ReleaseBindGroup(ref bindGroup2);
        }
    }
    
    private static readonly int Triangles_GPU_ShaderId      =  ShaderRegistry.NewShaderId("Triangles");
    private const  ulong        Triangles_GPU_layout_0_key  =  0x47;  // unique key set by Generator
    private const  ulong        Triangles_GPU_layout_2_key  =  0x11;  // unique key set by Generator
    
    private static ulong        Triangles_GPU_WgslHash      => 0x123; // support Hot-Relead
    
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache Triangles_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[3];
        var layout_0 = device.GetBindGroupLayout(Triangles_GPU_layout_0_key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutBuffer(0, BufferBindingType.ReadOnlyStorage);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex, Triangles_GPU_layout_0_key, "Triangles_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        layouts[1] = device.GetEmptyBindGroupLayout();
        
        var layout_2 = device.GetBindGroupLayout(Triangles_GPU_layout_2_key);
        if (!layout_2.IsCreated) {
            device.BindGroupLayoutUniform(0);
            layout_2 = device.CreateBindGroupLayout(ShaderStage.Vertex, Triangles_GPU_layout_2_key, "Triangles_layout_2"u8);
        }
        layouts[2] = layout_2;
        
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(RenderTest), Triangles_GPU_Shaders, "Triangles_pipeline"u8);
        
        var bindGroupCache = new Triangles_GPU_Cache();
        return ref device.CreatePipelineCache(Triangles_GPU_ShaderId, config, Triangles_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] Triangles_GPU_Shaders = [
        new("shaders/triangle.wgsl", vert: "vs_main", frag: "fs_main")
    ];
}