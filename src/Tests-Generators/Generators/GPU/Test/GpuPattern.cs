using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;
using Silk.NET.WebGPU;

// ReSharper disable UnusedParameter.Local
// ReSharper disable InconsistentNaming
namespace Tests.Generators.GPU;

public static class GpuPattern
{
    // generated shadow Method
    public static GpuBuffer<float> ShadowMethod(
        Buffer<float>   weight,
        Buffer<float>   input,
        float           bias,
        ExeType         exe,
        Buffer<float>   output = default)
    {
        if (exe == ExeType.GPU) {
            return ShadowMethod_GPU(weight, input, bias, output);
        }
        // Scalar / SIMD
        ShadowMethod_AVX(weight, input, bias, output);
        return null;
    }
    
    // generated AVX method
    private static void ShadowMethod_AVX(Buffer<float> weight, Buffer<float> input, float bias, Buffer<float> output) {
        // ...
    }
    
    // generated GPU method
    [SkipLocalsInit]
    private static GpuBuffer<float> ShadowMethod_GPU(Buffer<float> weight, Buffer<float> input, float bias, Buffer<float> output)
    {
        var paramState = new GpuBufferParams();
        paramState.Validate(weight, nameof(weight));
        paramState.Validate(input,  nameof(input));
        paramState.Validate(output, nameof(output));
        var device = paramState.GetDevice();
        
        var gpuOutput   = output.gpuBuffer ?? device.RentBuffer<float>(paramState.count);
        using var task  = device.RentTask();

        // Dependencies from inputs (out not Output!)
        if (weight.gpuBuffer.LastWritingTask != null) task.AddDependency(weight.gpuBuffer.LastWritingTask);
        if (input.gpuBuffer.LastWritingTask != null)  task.AddDependency(input.gpuBuffer.LastWritingTask);
        
        // Recording (task provides Encoder)
        var encoder = task.GetEncoder("ShadowMethod"u8);
        using (var pass = encoder.BeginComputePass("ShadowMethod"u8))
        {
            var effect = device.GetEffect(ShadowMethod_GPU_EffectSlot); // Each device has its own GpuEffect[] array
            if (!effect.IsCreated) {
                effect = ShadowMethod_GPU_CreateEffect(device);
            }
            pass.SetPipeline(effect.pipeline);
            
            var uniforms = new ShadowMethod_GPU_Uniforms {
                bias = bias,
                count = paramState.count
            };
            Span<BindGroupEntry> entries = stackalloc BindGroupEntry[3];
            entries[0] = GpuBindGroup.From  (0, weight);
            entries[1] = GpuBindGroup.From  (1, input);
            entries[2] = GpuBindGroup.From  (2, output);
            // TODO CreateBindGroup for buffers (storage) is expensive in wgpu => Cache it
            var bufferGroup = task.CreateBindGroup(effect.bufferLayout, entries, "ShadowMethod_buffers"u8);
            pass.SetBindGroup(0, bufferGroup);
            
            var entry = task.AsUniformEntry(0, uniforms);
            var uniformGroup = task.CreateBindGroup(effect.uniformLayout, entry, "ShadowMethod_uniforms"u8);
            pass.SetBindGroup(1, uniformGroup);
            
            pass.DispatchWorkgroups((input.Count + 63) / 64, 1, 1);        	// Execute ComputePass
            pass.End();                                                     // finish Pass (required by WebGPU State-Machine)
        }
        // connect task to output
        gpuOutput.LastWritingTask = task;
        task.Finish(encoder, "ShadowMethod"u8); // extract CommandBuffer from Encoder
        device.Enqueue(task);                      // queues CommandBuffer only. No Submit().

        gpuOutput.WaitInDebug();
        return gpuOutput;
    }
    
    private static readonly int ShadowMethod_GPU_EffectSlot = GpuDevice.NewGpuEffectSlot(); 
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static GpuEffect ShadowMethod_GPU_CreateEffect(GpuDevice device)
    {
        Span<GpuLayoutEntry> buffers = stackalloc GpuLayoutEntry[3];
        buffers[0] = GpuLayoutEntry.ReadOnlyStorage<float> (0); // @group(0) @binding(0) var<storage, read>       weight
        buffers[1] = GpuLayoutEntry.ReadOnlyStorage<float> (1); // @group(0) @binding(1) var<storage, read>       input
        buffers[2] = GpuLayoutEntry.ReadWriteStorage<float>(2); // @group(0) @binding(2) var<storage, read_write> output
        
        Span<GpuLayoutEntry> uniform = stackalloc GpuLayoutEntry[1];
        uniform[0] = GpuLayoutEntry.Uniform<float> (0);         // @group(1) @binding(0) var<uniform>             uniforms
        
        var bufferLayout    = device.CreateBindGroupLayout(buffers, "ShadowMethod_buffers"u8);
        var uniformLayout   = device.CreateBindGroupLayout(uniform, "ShadowMethod_uniforms"u8);
        var shaderModule    = device.CreateShaderModule(ShadowMethod_GPU_Shader(), "ShadowMethod"u8);
        var pipeline        = device.CreateComputePipeline(shaderModule, bufferLayout, uniformLayout, "ShadowMethod"u8);
        
        return device.CreateEffect(ShadowMethod_GPU_EffectSlot, bufferLayout, uniformLayout, pipeline);
    }

    // TODO in future the shader should be created at compile time. The binary will be "stored" as generated file (in memory)
    private static ReadOnlySpan<byte> ShadowMethod_GPU_Shader() =>
"""
struct ShadowMethod_Uniforms {
    bias    : f32,
    count   : u32
};

@group(0) @binding(0) var<storage, read>        weight:     array<f32>;
@group(0) @binding(1) var<storage, read>        input:      array<f32>;
@group(0) @binding(2) var<storage, read_write>  output:     array<f32>;

@group(1) @binding(0) var<uniform>              uniforms:   ShadowMethod_Uniforms;

@compute @workgroup_size(64)
fn ShadowMethod(@builtin(global_invocation_id) global_id: vec3<u32>) {
    let index = global_id.x;
    if (index >= uniforms.count) {
        return;
    }
    let weight_scalar = weight[index];
    // shader body generated from Blueprint method body
    output[index] = (input[index] * weight_scalar) + uniforms.bias;
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