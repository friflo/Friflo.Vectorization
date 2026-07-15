using System;
using System.Collections.Generic;
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
        
        ref readonly var pipelineCache = ref device.GetPipelineCache(MultiplyAdd_GPU_KernelId, MultiplyAdd_GPU_WgslHash); // Each device has its own GpuEffect[] array
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref MultiplyAdd_GPU_CreateComputeCache(device);
        }
        pass.SetPipeline(pipelineCache.computePipeline);
        
        var bindGroupCache = (MultiplyAdd_GPU_Cache)pipelineCache.bindGroupCache;
            
        var key = (weight.Handle, input.Handle, output.Handle);
        if (!bindGroupCache.bufferGroup.TryGetValue(key, out var bufferGroup)) {
            recorder.BindGroupEntryBuffer(0, weight.Buffer);
            recorder.BindGroupEntryBuffer(1, input.Buffer);
            recorder.BindGroupEntryBuffer(2, output.Buffer);
            bufferGroup = recorder.CreateBindGroup(pipelineCache.bufferLayout, "MultiplyAdd_buffers"u8);
            bindGroupCache.bufferGroup.Add(key, bufferGroup);
        }
        pass.SetBindGroup(0, bufferGroup);
            
        var uniforms = new MultiplyAdd_GPU_Uniforms {
            count       = buffers.length,
            weight_off  = weight.Offset,
            input_off   = input .Offset,
            output_off  = output.Offset,
            bias        = bias
        };
        pass.SetUniformBindGroup(1, ref bindGroupCache.uniformGroup, pipelineCache, uniforms, "MultiplyAdd_uniforms"u8);
            
        pass.DispatchWorkgroups((buffers.length + 63) / 64, 1, 1);
    }
    
    private sealed class MultiplyAdd_GPU_Cache : BindGroupCache
    {
        internal readonly   Dictionary<(nint,nint,nint), WgpuBindGroup> bufferGroup = new ();
        internal            WgpuBindGroup                               uniformGroup;
        
        protected override void Clear() {
            ReleaseBindGroups(bufferGroup);
            ReleaseBindGroup(ref uniformGroup);
        }
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
    private static ref readonly ComputeCache MultiplyAdd_GPU_CreateComputeCache(WgpuDevice device)
    {
        var bufferLayout = device.GetBindGroupLayout(MultiplyAdd_GPU_BufferLayoutKey);
        if (!bufferLayout.IsCreated) {
            device.BindGroupLayoutBuffer(0, BufferBindingType.ReadOnlyStorage);
            device.BindGroupLayoutBuffer(1, BufferBindingType.ReadOnlyStorage);
            device.BindGroupLayoutBuffer(2, BufferBindingType.Storage);
            bufferLayout = device.CreateBindGroupLayout(ShaderStage.Compute, MultiplyAdd_GPU_BufferLayoutKey, "MultiplyAdd_buffers"u8);
        }
        var uniformLayout = device.GetBindGroupLayout(MultiplyAdd_GPU_UniformLayoutKey);
        if (!uniformLayout.IsCreated) {
            device.BindGroupLayoutUniform(0);
            uniformLayout   = device.CreateBindGroupLayout(ShaderStage.Compute, MultiplyAdd_GPU_UniformLayoutKey, "MultiplyAdd_uniforms"u8);
        }
        using var shaderModule  = device.CreateShaderModule(MultiplyAdd_GPU_Shader(), "MultiplyAdd"u8);
        var pipeline            = device.CreateComputePipeline(shaderModule, bufferLayout, uniformLayout, "MultiplyAdd"u8);
        
        var bindGroupCache = new MultiplyAdd_GPU_Cache();
        return ref device.CreatePipelineCache(MultiplyAdd_GPU_KernelId, MultiplyAdd_GPU_WgslHash, pipeline, bufferLayout, uniformLayout, bindGroupCache);
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