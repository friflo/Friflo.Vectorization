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

public static class WebGPUPattern
{
    [SkipLocalsInit] // Lock-free GPU kernel with deferred, hazard-driven pass batching
    internal static void ShadowMethod_GPU(
        in GpuBuffers       buffers,
        in InBuffer<float>  weight_,
        in InBuffer<float>  input_,
        in float            bias,
        in Buffer<float>    output_)
    {
        var device      = (WgpuDevice)buffers.device;
        var recorder    = device.Recorder;
        recorder.Init(ShadowMethod_GPU_EffectSlot);
        
        var input       = recorder.RequireRead     (input_);
        var weight      = recorder.RequireRead     (weight_);
        var output      = recorder.RequireReadWrite(output_);

        using (var pass = recorder.BeginComputePass("ShadowMethod"u8))
        {
            ref var effect = ref device.GetEffect(ShadowMethod_GPU_EffectSlot); // Each device has its own GpuEffect[] array
            if (!effect.IsCreated) {
                effect = ref ShadowMethod_GPU_CreateEffect(device);
            }
            pass.SetPipeline(effect.pipeline);
            
            // Creation of a buffer bind group is expensive in wgpu. So we cache them. Cache has two entries.
            var bufferGroup = effect.bufferCache.GetGroup(buffers.hash);
            if (!bufferGroup.IsCreated) {
                Span<BindGroupEntry> entries = stackalloc BindGroupEntry[3];
                entries[0] = WgpuBindGroup.From  (0, weight);
                entries[1] = WgpuBindGroup.From  (1, input);
                entries[2] = WgpuBindGroup.From  (2, output);
                bufferGroup = recorder.CreateBindGroup(effect.bufferLayout, entries, "ShadowMethod_buffers"u8);
                device.UpdateBufferCache(ShadowMethod_GPU_EffectSlot, bufferGroup, buffers.hash);
            }
            pass.SetBindGroup0(bufferGroup, buffers.hash);
            
            var uniforms = new ShadowMethod_GPU_Uniforms {
                count       = buffers.length,
                weight_off  = weight_.Offset,
                input_off   = input_ .Offset,
                output_off  = output_.Offset,
                bias        = bias
            };
            var entry = recorder.AsUniformEntry(0, uniforms);
            // Creation of a uniform bind group is much cheaper than for a buffer in wgpu. So no caching.
            // TODO: Use Dynamic Offsets to move CreateBindGroup out of the loop and use pass.SetBindGroup1Dynamic instead.
            var uniformGroup = recorder.CreateBindGroup(effect.uniformLayout, entry, "ShadowMethod_uniforms"u8);
            pass.SetBindGroup1(uniformGroup);
            
            pass.DispatchWorkgroups((buffers.length + 63) / 64, 1, 1);
        }
        recorder.TrackWrite(output_);

        device.WaitInDebug();
    }
    
    [StructLayout(LayoutKind.Explicit, Size = 32)]  // WGSL uses std140/std430 Layout
    private struct ShadowMethod_GPU_Uniforms
    {
        [FieldOffset( 0)]    public int      count;
        [FieldOffset( 4)]    public float    bias;
        [FieldOffset( 8)]    public int      weight_off;
        [FieldOffset(12)]    public int      input_off;
        [FieldOffset(16)]    public int      output_off;
    }
    
    private static readonly int ShadowMethod_GPU_EffectSlot         = KernelRegistry.NewKernelId("ShadowMethod");
    private const ulong         ShadowMethod_GPU_BufferLayoutKey    = 1337; // unique key set by Generator
    private const ulong         ShadowMethod_GPU_UniformLayoutKey   = 42;   // unique key set by Generator

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref WgpuEffect ShadowMethod_GPU_CreateEffect(WgpuDevice device)
    {
        var bufferLayout = device.GetBindGroupLayout(ShadowMethod_GPU_BufferLayoutKey);
        if (!bufferLayout.IsCreated) {
            Span<WgpuLayoutEntry> buffers = stackalloc WgpuLayoutEntry[3];
            buffers[0] = WgpuLayoutEntry.ReadOnlyStorage (0);   // var<storage, read>       weight_arr:     array<f32>;
            buffers[1] = WgpuLayoutEntry.ReadOnlyStorage (1);   // var<storage, read>       input_arr:      array<f32>;
            buffers[2] = WgpuLayoutEntry.ReadWriteStorage(2);   // var<storage, read_write> output_arr:     array<f32>;
            bufferLayout = device.CreateBindGroupLayout(buffers, ShadowMethod_GPU_BufferLayoutKey, "ShadowMethod_buffers"u8);
        }
        var uniformLayout = device.GetBindGroupLayout(ShadowMethod_GPU_UniformLayoutKey);
        if (!uniformLayout.IsCreated) {
            Span<WgpuLayoutEntry> uniform = stackalloc WgpuLayoutEntry[1];
            uniform[0] = WgpuLayoutEntry.Uniform(0);            // var<uniform>              uniforms
            uniformLayout   = device.CreateBindGroupLayout(uniform, ShadowMethod_GPU_UniformLayoutKey, "ShadowMethod_uniforms"u8);
        }
        var shaderModule    = device.CreateShaderModule(ShadowMethod_GPU_Shader(), "ShadowMethod"u8);
        var pipeline        = device.CreateComputePipeline(shaderModule, bufferLayout, uniformLayout, "ShadowMethod"u8);
        
        return ref device.CreateEffect(ShadowMethod_GPU_EffectSlot, pipeline, bufferLayout, uniformLayout);
    }

    // TODO in future the shader should be created at compile time. The binary will be "stored" as generated file (in memory)
    private static ReadOnlySpan<byte> ShadowMethod_GPU_Shader() =>
"""
struct ShadowMethod_Uniforms {
    count       : u32,
    bias        : f32,
    weight_off  : u32,
    input_off   : u32,
    output_off  : u32
};

@group(0) @binding(0) var<storage, read>        weight_arr:     array<f32>;
@group(0) @binding(1) var<storage, read>        input_arr:      array<f32>;
@group(0) @binding(2) var<storage, read_write>  output_arr:     array<f32>;

@group(1) @binding(0) var<uniform>              uniforms:   	ShadowMethod_Uniforms;

@compute @workgroup_size(64)
fn ShadowMethod(@builtin(global_invocation_id) global_id: vec3<u32>) {
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