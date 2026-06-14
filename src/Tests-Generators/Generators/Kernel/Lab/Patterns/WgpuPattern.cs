using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable UnusedParameter.Local
// ReSharper disable InconsistentNaming
namespace Kernel.Lab;

public static class WgpuPattern
{
    // Lock-free, zero-alloc GPU kernel with deferred, on-the-fly hazard-driven pass batching
    internal static void MultiplyAdd_GPU(
        in GpuBuffers           buffers,
        in InBuffer<float>      weight,
        in InBuffer<float>      input,
        in float                bias,
        in InOutBuffer<float>   output)
    {
        var device      = (WgpuDevice)buffers.device;
        var recorder    = device.Recorder; 			// Recorder == thread context
        recorder.Init(MultiplyAdd_GPU_KernelId, "MultiplyAdd"u8);
        
        recorder.RequireRead     (weight);
        recorder.RequireRead     (input);
        recorder.RequireReadWrite(output);

        using var pass = recorder.BeginComputePass("MultiplyAdd"u8);
        
        ref var effect = ref device.GetComputeEffect(MultiplyAdd_GPU_KernelId, MultiplyAdd_GPU_WgslHash); // Each device has its own GpuEffect[] array
        if (!effect.IsCreated) {
            effect = ref MultiplyAdd_GPU_CreateEffect(device);
        }
        pass.SetPipeline(effect.pipeline);
            
        // Creation of a buffer bind group is expensive in wgpu. So we cache them. Cache has two entries.
        var bufferGroup = effect.computeBufferCache.GetGroup(buffers.hash);
        if (!bufferGroup.IsCreated) {
            Span<BindGroupEntry> entries = stackalloc BindGroupEntry[3];
            entries[0] = WgpuBindGroup.From  (0, weight.Buffer);
            entries[1] = WgpuBindGroup.From  (1, input.Buffer);
            entries[2] = WgpuBindGroup.From  (2, output.Buffer);
            bufferGroup = recorder.CreateBindGroup(effect.bufferLayout, entries, "MultiplyAdd_buffers"u8);
            device.UpdateComputeCache(MultiplyAdd_GPU_KernelId, bufferGroup, buffers.hash);
        }
        pass.SetBindGroup(0, bufferGroup, buffers.hash);
            
        var uniforms = new MultiplyAdd_GPU_Uniforms {
            count       = buffers.length,
            weight_off  = weight.Offset,
            input_off   = input .Offset,
            output_off  = output.Offset,
            bias        = bias
        };
        pass.SetUniformBindGroup(1, ref effect, uniforms, "MultiplyAdd_uniforms"u8);
            
        pass.DispatchWorkgroups((buffers.length + 63) / 64, 1, 1);
    }
    
    [StructLayout(LayoutKind.Explicit, Size = 32)]  // WGSL uses std140/std430 Layout
    private struct MultiplyAdd_GPU_Uniforms
    {
        [FieldOffset( 0)]    public int      count;
        [FieldOffset( 4)]    public float    bias;
        [FieldOffset( 8)]    public int      weight_off;
        [FieldOffset(12)]    public int      input_off;
        [FieldOffset(16)]    public int      output_off;
    }
    
    private static readonly int MultiplyAdd_GPU_KernelId            =  KernelRegistry.NewKernelId("MultiplyAddKernel");
    private const ulong         MultiplyAdd_GPU_BufferLayoutKey     =  0x1337;  // unique key set by Generator
    private const ulong         MultiplyAdd_GPU_UniformLayoutKey    =  0x42;    // unique key set by Generator
    private static ulong        MultiplyAdd_GPU_WgslHash            => 0x777;   // support Hot-Relead

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref WgpuComputeEffect MultiplyAdd_GPU_CreateEffect(WgpuDevice device)
    {
        var bufferLayout = device.GetBindGroupLayout(MultiplyAdd_GPU_BufferLayoutKey);
        if (!bufferLayout.IsCreated) {
            Span<WgpuLayoutEntry> buffers = stackalloc WgpuLayoutEntry[3];
            buffers[0] = WgpuLayoutEntry.ReadOnlyStorage (0);   // var<storage, read>       weight_arr:     array<f32>;
            buffers[1] = WgpuLayoutEntry.ReadOnlyStorage (1);   // var<storage, read>       input_arr:      array<f32>;
            buffers[2] = WgpuLayoutEntry.ReadWriteStorage(2);   // var<storage, read_write> output_arr:     array<f32>;
            bufferLayout = device.CreateBindGroupLayout(buffers, ShaderStage.Compute, false, MultiplyAdd_GPU_BufferLayoutKey, "MultiplyAdd_buffers"u8);
        }
        var uniformLayout = device.GetBindGroupLayout(MultiplyAdd_GPU_UniformLayoutKey);
        if (!uniformLayout.IsCreated) {
            Span<WgpuLayoutEntry> uniform = stackalloc WgpuLayoutEntry[1];
            uniform[0] = WgpuLayoutEntry.Uniform(0);            // var<uniform>              uniforms
            uniformLayout   = device.CreateBindGroupLayout(uniform, ShaderStage.Compute, true, MultiplyAdd_GPU_UniformLayoutKey, "MultiplyAdd_uniforms"u8);
        }
        var shaderModule    = device.CreateShaderModule(MultiplyAdd_GPU_Shader(), "MultiplyAdd"u8);
        var pipeline        = device.CreateComputePipeline(shaderModule, bufferLayout, uniformLayout, "MultiplyAdd"u8);
        
        return ref device.CreateComputeEffect(MultiplyAdd_GPU_KernelId, MultiplyAdd_GPU_WgslHash, pipeline, default, bufferLayout, uniformLayout);
    }

    // TODO in future the shader should be created at compile time. The binary will be "stored" as generated file (in memory)
    private static ReadOnlySpan<byte> MultiplyAdd_GPU_Shader() =>
"""
struct MultiplyAdd_GPU_Uniforms {
    count       : u32,
    bias        : f32,
    weight_off  : u32,
    input_off   : u32,
    output_off  : u32
};

@group(0) @binding(0) var<storage, read>        weight_arr:     array<f32>;
@group(0) @binding(1) var<storage, read>        input_arr:      array<f32>;
@group(0) @binding(2) var<storage, read_write>  output_arr:     array<f32>;

@group(1) @binding(0) var<uniform>              uniforms:   	MultiplyAdd_GPU_Uniforms;

@compute @workgroup_size(64)
fn MultiplyAdd(@builtin(global_invocation_id) global_id: vec3<u32>) {
    let index = global_id.x;
    if (index >= uniforms.count) {
        return;
    }
    let weight = weight_arr[uniforms.weight_off + index];
    let input  = input_arr [uniforms.input_off  + index];
    
    let output = (weight * input) + uniforms.bias;

    output_arr[uniforms.output_off + index] = output;
}
"""u8;

}