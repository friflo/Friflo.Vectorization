using System;
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
        float           uniform,
        ExeType         exe,
        Buffer<float>   output = default)
    {
        if (exe == ExeType.GPU) {
            return ShadowMethod_GPU(weight, input, uniform, output);
        }
        // Scalar / SIMD
        ShadowMethod_AVX(weight, input, uniform, output);
        return null;
    }
    
    // generated AVX method
    private static void ShadowMethod_AVX(Buffer<float> weight, Buffer<float> input, float uniform, Buffer<float> output) {
        // ...
    }
    
    // generated GPU method
    private static GpuBuffer<float> ShadowMethod_GPU(Buffer<float> weight, Buffer<float> input, float uniform, Buffer<float> output)
    {
        var paramState = new GpuParamState();
        paramState.Validate(weight, nameof(weight));
        paramState.Validate(input,  nameof(input));
        paramState.Validate(output, nameof(output));
        var dev = paramState.GetDevice();
        
        var gpuOutput   = output.gpuBuffer ?? dev.RentBuffer<float>(input.Length);
        using var task  = dev.RentTask();

        // Dependencies from inputs (out not Output!)
        if (weight.gpuBuffer.LastWritingTask != null) task.AddDependency(weight.gpuBuffer.LastWritingTask);
        if (input.gpuBuffer.LastWritingTask != null)  task.AddDependency(input.gpuBuffer.LastWritingTask);
        
        // Recording (task provides Encoder)
        var encoder = task.GetEncoder("ShadowMethod"u8);
        using (var pass = encoder.BeginComputePass("ShadowMethod"u8))
        {
            var gpuEffect = ShadowMethod_GPU_GetGpuEffect(dev);
            pass.SetPipeline(gpuEffect.pipeline);
            
            var uniforms = new ShadowMethod_Uniforms { uniform = uniform };
            Span<BindGroupEntry> entries = stackalloc BindGroupEntry[4];
            entries[0] = GpuBindGroup.From  (0, weight.gpuBuffer);
            entries[1] = GpuBindGroup.From  (1, input.gpuBuffer);
            entries[2] = task.AsUniformEntry(2, uniforms);
            entries[3] = GpuBindGroup.From  (3, output.gpuBuffer);
            
            var bindGroup = task.CreateBindGroup(gpuEffect.layout, entries, "ShadowMethod"u8);
            pass.SetBindGroup(0, bindGroup);
            pass.DispatchWorkgroups((input.Length + 63) / 64, 1, 1);        // Execute ComputePass
            pass.End();                                                     // finish Pass (required by WebGPU State-Machine)
        }
        // connect task to output
        gpuOutput.LastWritingTask = task;
        task.Finish(encoder, "ShadowMethod"u8); // extract CommandBuffer from Encoder
        dev.Enqueue(task);                      // queues CommandBuffer only. No Submit().

        gpuOutput.WaitInDebug();
        return gpuOutput;
    }
    
    private static readonly int ShadowMethod_GpuEffectSlot = GpuDevice.NewGpuEffectSlot(); 
    
    private static GpuEffect ShadowMethod_GPU_GetGpuEffect(GpuDevice device)
    {
        // Each device has its own GpuEffect[] array
        var gpuEffect = device.GetGpuEffect(ShadowMethod_GpuEffectSlot); // array index lookup

        if (gpuEffect.IsCreated) {
            return gpuEffect;
        }
        Span<GpuLayoutEntry> entries = stackalloc GpuLayoutEntry[4];
        entries[0] = GpuLayoutEntry.ReadOnlyStorage<float> (0); // @binding(0) var<storage, read>       weight
        entries[1] = GpuLayoutEntry.ReadOnlyStorage<float> (1); // @binding(1) var<storage, read>       input
        entries[2] = GpuLayoutEntry.Uniform<float>         (2); // @binding(2) var<uniform>             uniforms
        entries[3] = GpuLayoutEntry.ReadWriteStorage<float>(3); // @binding(3) var<storage, read_write> output
        
        var layout          = device.CreateBindGroupLayout(entries, "ShadowMethod"u8);
        var shaderModule    = device.CreateShaderModule(ShadowMethod_GPU_Shader(), "ShadowMethod"u8);
        var pipeline        = device.CreateComputePipeline(shaderModule, "main"u8, layout, "ShadowMethod"u8);
        
        gpuEffect = new GpuEffect(layout, shaderModule, pipeline);
        device.SetGpuEffect(ShadowMethod_GpuEffectSlot, gpuEffect);
        return gpuEffect;
    }

    // TODO in future the shader should be created at compile time. The binary will be "stored" as generated file (in memory)
    private static ReadOnlySpan<byte> ShadowMethod_GPU_Shader() =>
"""
struct ShadowMethod_Uniforms {
    uniform : f32,
};

@group(0) @binding(0) var<storage, read>        weight:     array<f32>;
@group(0) @binding(1) var<storage, read>        input:      array<f32>;
@group(0) @binding(2) var<uniform>              uniforms:   ShadowMethod_Uniforms;
@group(0) @binding(3) var<storage, read_write>  output:     array<f32>;

@compute @workgroup_size(64)
fn main(@builtin(global_invocation_id) global_id: vec3<u32>) {
    let index = global_id.x;
    
    let weight_scalar = weight[index];
    // shader body generated from Blueprint method body
    output[index] = (input[index] * weight_scalar) + uniforms.uniform;
}
"""u8;
    
    // struct for uniforms
    [StructLayout(LayoutKind.Explicit, Size = 16)]  // WGSL uses std140/std430 Layout. Fill up to 16 bytes
    private struct ShadowMethod_Uniforms
    {
        [FieldOffset(0)] public float uniform;
    //  public float uniform2;
    //  public int   iteration;
    }
}