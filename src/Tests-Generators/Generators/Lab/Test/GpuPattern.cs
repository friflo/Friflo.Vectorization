using System;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;

// ReSharper disable UnusedParameter.Local
// ReSharper disable InconsistentNaming
namespace Tests.Generators.Lab;

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
        var ctx = paramState.GetContext();
        
        var gpuOutput   = output.gpuBuffer ?? ctx.RentBuffer<float>(input.Length);
        using var task  = ctx.RentTask();

        // Dependencies from inputs (out not Output!)
        if (weight.gpuBuffer.LastWritingTask != null) task.AddDependency(weight.gpuBuffer.LastWritingTask);
        if (input.gpuBuffer.LastWritingTask != null)  task.AddDependency(input.gpuBuffer.LastWritingTask);
        
        // Recording (task provides Encoder)
        var encoder = task.GetEncoder(ctx); 
        using (var pass = encoder.BeginComputePass())
        {
            var gpuEffect = ShadowMethod_GPU_GetGpuEffect(ctx);
            pass.SetPipeline(gpuEffect.pipeline);
            
            var uniforms = new ShadowMethod_Uniforms { uniform = uniform };
            Span<GpuBindEntry> entries = stackalloc GpuBindEntry[4];
            entries[0] = GpuBindEntry.From(0, weight.gpuBuffer);
            entries[1] = GpuBindEntry.From(1, input.gpuBuffer);
            entries[2] = task.AsUniformEntry(2, uniforms);
            entries[3] = GpuBindEntry.From(3, output.gpuBuffer);
            
            var bindGroup = task.CreateBindGroup(gpuEffect.layout, entries);
            pass.SetBindGroup(0, bindGroup);
            pass.DispatchWorkgroups((input.Length + 63) / 64, 1, 1);        // Execute ComputePass
            pass.End();                                                     // finish Pass (required by WebGPU State-Machine)
        }
        // connect task to output
        gpuOutput.LastWritingTask = task;
        task.Finish(encoder);   // extract CommandBuffer from Encoder
        ctx.Enqueue(task);      // queues CommandBuffer only. No Submit().

        gpuOutput.WaitInDebug();
        return gpuOutput;
    }
    
    private static readonly int ShadowMethod_GpuEffectSlot = GpuContext.NewGpuEffectSlot(); 
    
    private static GpuEffect ShadowMethod_GPU_GetGpuEffect(GpuContext ctx)
    {
        var gpuEffect = ctx.GetGpuEffect(ShadowMethod_GpuEffectSlot); // array index lookup

        if (gpuEffect.isCreated) {
            return gpuEffect;
        }
        Span<GpuLayoutEntry> entries = stackalloc GpuLayoutEntry[4];
        entries[0] = GpuLayoutEntry.ReadOnlyStorage<float> (0); // @binding(0) var<storage, read>       weight
        entries[1] = GpuLayoutEntry.ReadOnlyStorage<float> (1); // @binding(1) var<storage, read>       input
        entries[2] = GpuLayoutEntry.Uniform<float>         (2); // @binding(2) var<uniform>             uniforms
        entries[3] = GpuLayoutEntry.ReadWriteStorage<float>(3); // @binding(3) var<storage, read_write> output
        
        var layout          = ctx.CreateBindGroupLayout("ShadowMethod_GPU"u8, entries);
        var shaderModule    = ctx.CreateShaderModule(ShadowMethod_GPU_Shader());
        var pipeline        = ctx.CreateComputePipeline(shaderModule, "main"u8, layout);
        
        gpuEffect = new GpuEffect(layout, pipeline);
        ctx.SetGpuEffect(ShadowMethod_GpuEffectSlot, gpuEffect);
        return gpuEffect;
    }

    // TODO in future the shader should be created at compile time. The binary will be "stored" as generated file (in memory)
    private static ReadOnlySpan<byte> ShadowMethod_GPU_Shader() => @"
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
}"u8;
    
    // struct for uniforms
    [StructLayout(LayoutKind.Explicit, Size = 16)]  // WGSL uses std140/std430 Layout. Fill up to 16 bytes
    private struct ShadowMethod_Uniforms
    {
        [FieldOffset(0)] public float uniform;
    //  public float uniform2;
    //  public int   iteration;
    }
}