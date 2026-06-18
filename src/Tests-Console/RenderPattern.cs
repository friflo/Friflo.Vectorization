using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedParameter.Local
// ReSharper disable UnusedMember.Local
// ReSharper disable FieldCanBeMadeReadOnly.Global
namespace TestConsole;

public partial class RenderTest
{
    /// generated method body
    static partial void DrawTriangles(
        RenderPass<MainWorld>   renderPass,
        InBuffer<VertexData>    triangles,
        MyUniform               myUniform,
        RenderConfig    		config)
	{
        var buffers =
        GpuBuffers.Create(triangles, nameof(triangles));
        
        var pass        = renderPass.Value;
		var recorder	= pass.recorder;
		var device		= recorder.Device;
		recorder.Init(Triangles_GPU_ShaderId, "Triangles"u8);
        
        recorder.RequireRead(triangles);

        ref var effect = ref device.GetShaderEffect(Triangles_GPU_ShaderId, Triangles_GPU_WgslHash); // Each device has its own GpuEffect[] array
        if (!effect.IsCreated) {
            effect = ref Triangles_GPU_CreateEffect(device, config);
        }
        pass.SetPipeline(effect.renderPipeline);
        
        // Creation of a buffer bind group is expensive in wgpu. So we cache them. Cache has two entries.
        var bufferGroup = effect.bufferCache.GetGroup(buffers.hash);
        if (!bufferGroup.IsCreated) {
            Span<BindGroupEntry> entries = stackalloc BindGroupEntry[1];
            entries[0] = WgpuBindGroup.From  (0, triangles.Buffer);
            bufferGroup = recorder.CreateBindGroup(effect.bufferLayout, entries, "TriangleStorage"u8);
            device.UpdateShaderCache(Triangles_GPU_ShaderId, bufferGroup, buffers.hash);
        }
        pass.SetBindGroup(0, bufferGroup, buffers.hash);
        
        pass.SetUniformBindGroup(1, ref effect, myUniform, "MyUniforms"u8);
        
        pass.Draw(buffers.length, 1, triangles.Offset, 0);
	}
    
    private static readonly int Triangles_GPU_ShaderId            =  ShaderRegistry.NewShaderId("TrianglesShader");
    private const ulong         Triangles_GPU_BufferLayoutKey     =  0x47;  // unique key set by Generator
    private const ulong         Triangles_GPU_UniformLayoutKey    =  0x11;  // unique key set by Generator
    private static ulong        Triangles_GPU_WgslHash            => 0x123; // support Hot-Relead
    
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref WgpuShaderEffect Triangles_GPU_CreateEffect(WgpuDevice device, RenderConfig config)
    {
        var bufferLayout = device.GetBindGroupLayout(Triangles_GPU_BufferLayoutKey);
        if (!bufferLayout.IsCreated) {
            Span<WgpuLayoutEntry> buffers = stackalloc WgpuLayoutEntry[1];
            buffers[0] = WgpuLayoutEntry.ReadOnlyStorage (0);   // @group(0) @binding(0) var<storage, read> mesh_data: TriangleStorage;
            bufferLayout = device.CreateBindGroupLayout(buffers, ShaderStage.Vertex, false, Triangles_GPU_BufferLayoutKey, "TriangleStorage"u8);
        }
        var uniformLayout = device.GetBindGroupLayout(Triangles_GPU_UniformLayoutKey);
        if (!uniformLayout.IsCreated) {
            Span<WgpuLayoutEntry> uniform = stackalloc WgpuLayoutEntry[1];
            uniform[0] = WgpuLayoutEntry.Uniform(0);            // @group(1) @binding(0) var<uniform>          myUniforms: MyUniforms;
            uniformLayout = device.CreateBindGroupLayout(uniform, ShaderStage.Vertex, true, Triangles_GPU_UniformLayoutKey, "MyUniforms"u8);
        }
        var shaderModule = device.CreateShaderModule(Triangles_GPU_Shader(), "Triangles"u8);
        
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[2];
        layouts[0] = bufferLayout;
        layouts[1] = uniformLayout;

        var pipeline = device.CreateRenderPipeline(shaderModule, layouts, config, "vs_main"u8, "fs_main"u8, "Triangles"u8);
        
        return ref device.CreateShaderEffect(Triangles_GPU_ShaderId, Triangles_GPU_WgslHash, pipeline, bufferLayout, uniformLayout);
    }
    
    private static ReadOnlySpan<byte> Triangles_GPU_Shader() => WgpuResource.GetResource(typeof(RenderTest).Assembly, "Tests-Console.Shaders.triangle.wgsl");
}