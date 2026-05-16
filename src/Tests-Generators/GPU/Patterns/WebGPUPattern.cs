using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable UnusedParameter.Local
// ReSharper disable InconsistentNaming
namespace Tests.GPU;

public static class WebGPUPattern
{
    // generated GPU method
    [SkipLocalsInit]
    internal static GpuBuffer<float> ShadowMethod_GPU(
        in GpuBuffers       buffers,
        GpuBuffer<float>    weight,
        GpuBuffer<float>    input,
        float               bias,
        GpuBuffer<float>    output)
    {
        var device      = (WgpuDevice)buffers.GetDevice();
        output ??= device.RentBuffer<float>(buffers.count);
        using var task  = device.RentTask();

        // Dependencies from inputs (out not Output!)
        if (weight.LastWritingTask != null) task.AddDependency(weight);
        if (input.LastWritingTask != null)  task.AddDependency(input);
        
        // Recording (task provides Encoder)
        var encoder = task.GetEncoder("ShadowMethod"u8);
        using (var pass = encoder.BeginComputePass("ShadowMethod"u8))
        {
            var effect = device.GetEffect(ShadowMethod_GPU_EffectSlot); // Each device has its own GpuEffect[] array
            if (!effect.IsCreated) {
                effect = ShadowMethod_GPU_CreateEffect(device);
            }
            pass.SetPipeline(effect.pipeline);
            
            // Creation of a buffer bind group is expensive in wgpu. So we cache them. Cache has two entries.
            var bufferGroup = effect.bufferCache.GetGroup(buffers.hash);
            if (!bufferGroup.IsCreated) {
                Span<BindGroupEntry> entries = stackalloc BindGroupEntry[3];
                entries[0] = WgpuBindGroup.From  (0, weight);
                entries[1] = WgpuBindGroup.From  (1, input);
                entries[2] = WgpuBindGroup.From  (2, output);
                bufferGroup = task.CreateBindGroup(effect.bufferLayout, entries, "ShadowMethod_buffers"u8);
                device.UpdateBufferCache(ShadowMethod_GPU_EffectSlot, bufferGroup, buffers.hash);
            }
            pass.SetBindGroup(0, bufferGroup);
            
            var uniforms = new ShadowMethod_GPU_Uniforms {
                bias = bias,
                count = buffers.count
            };
            var entry = task.AsUniformEntry(0, uniforms);
            // Creation of a uniform bind group is much cheaper than for a buffer in wgpu. So no caching.
            var uniformGroup = task.CreateBindGroup(effect.uniformLayout, entry, "ShadowMethod_uniforms"u8);
            pass.SetBindGroup(1, uniformGroup);
            
            pass.DispatchWorkgroups((buffers.count + 63) / 64, 1, 1);       // Execute ComputePass
            pass.End();                                                     // finish Pass (required by WebGPU State-Machine)
        }
        // connect task to output
        ((WgpuBuffer<float>)output).SetLastWritingTask(task);
        task.Finish(encoder, "ShadowMethod"u8); // extract CommandBuffer from Encoder
        device.Enqueue(task);                      // queues CommandBuffer only. No Submit().

        output.WaitInDebug();
        return output;
    }
    
    private static readonly int ShadowMethod_GPU_EffectSlot         = WgpuDevice.NewEffectSlot();
    private const ulong         ShadowMethod_GPU_BufferLayoutKey    = 1337; // unique hash key calculated by Generator
    private const ulong         ShadowMethod_GPU_UniformLayoutKey   = 42; // unique hash key calculated by Generator

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WgpuEffect ShadowMethod_GPU_CreateEffect(WgpuDevice device)
    {
        var bufferLayout = device.GetBindGroupLayout(ShadowMethod_GPU_BufferLayoutKey);
        if (!bufferLayout.IsCreated) {
            Span<WgpuLayoutEntry> buffers = stackalloc WgpuLayoutEntry[3];
            buffers[0] = WgpuLayoutEntry.ReadOnlyStorage<float> (0); // @group(0) @binding(0) var<storage, read>       weight
            buffers[1] = WgpuLayoutEntry.ReadOnlyStorage<float> (1); // @group(0) @binding(1) var<storage, read>       input
            buffers[2] = WgpuLayoutEntry.ReadWriteStorage<float>(2); // @group(0) @binding(2) var<storage, read_write> output
            bufferLayout = device.CreateBindGroupLayout(buffers, ShadowMethod_GPU_BufferLayoutKey, "ShadowMethod_buffers"u8);
        }
        var uniformLayout = device.GetBindGroupLayout(ShadowMethod_GPU_UniformLayoutKey);
        if (!uniformLayout.IsCreated) {
            Span<WgpuLayoutEntry> uniform = stackalloc WgpuLayoutEntry[1];
            uniform[0] = WgpuLayoutEntry.Uniform<ShadowMethod_GPU_Uniforms> (0); // @group(1)
            uniformLayout   = device.CreateBindGroupLayout(uniform, ShadowMethod_GPU_UniformLayoutKey, "ShadowMethod_uniforms"u8);
        }
        var shaderModule    = device.CreateShaderModule(ShadowMethod_GPU_Shader(), "ShadowMethod"u8);
        var pipeline        = device.CreateComputePipeline(shaderModule, bufferLayout, uniformLayout, "ShadowMethod"u8);
        
        return device.CreateEffect(ShadowMethod_GPU_EffectSlot, pipeline, bufferLayout, uniformLayout);
    }

    // TODO in future the shader should be created at compile time. The binary will be "stored" as generated file (in memory)
    private static ReadOnlySpan<byte> ShadowMethod_GPU_Shader() =>
"""
struct ShadowMethod_Uniforms {
    bias    : f32,
    count   : u32
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
    let weight = weight_arr[index];
    let input  = input_arr[index];
    // shader body generated from Blueprint method body
    output_arr[index] = (input * weight) + uniforms.bias;
}
"""u8;
    
    // struct for uniforms
    [StructLayout(LayoutKind.Explicit, Size = 16)]  // WGSL uses std140/std430 Layout
    private struct ShadowMethod_GPU_Uniforms
    {
        [FieldOffset(0)]    public float    bias;
        [FieldOffset(4)]    public int      count;
    //  public float uniform2;
    //  public int   iteration;
    }
}