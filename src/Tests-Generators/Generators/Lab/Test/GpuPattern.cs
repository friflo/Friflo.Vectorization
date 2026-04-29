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
            pass.SetPipeline(gpuEffect.Pipeline);                  // Set Pipeline to "MyShader"
            
            var uniforms  = new ShadowMethod_Uniforms { uniform = uniform };
            var bindGroup = ctx.CreateBindGroup(gpuEffect.Layout, [ // CreateBindGroup uses Span<GpuBindEntry> parameter
                GpuBindEntry.From (0, weight.gpuBuffer),
                GpuBindEntry.From (1, input.gpuBuffer),
                ctx.AsUniformEntry(2, uniforms),
                GpuBindEntry.From (3, output.gpuBuffer),
            ]);
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

        if (gpuEffect != null) {
            return gpuEffect;
        }
        var layout = ctx.BindGroupLayoutBuilder()       //   TODO Allocates BindGroupLayoutBuilder - check if it can be reused 
            .AddReadOnlyBuffer<float> (0, "weight")     // @binding(0) var<storage, read>       weight
            .AddReadOnlyBuffer<float> (1, "input")      // @binding(1) var<storage, read>       input
            .AddUniform<float>        (2, "uniform")    // @binding(2) var<uniform>             uniforms
            .AddBuffer<float>         (3, "output")     // @binding(3) var<storage, read_write> output
            .Build("ShadowMethod_GPU\0"u8); // Build() pins the literal 
        
        var shaderModule    = ctx.CreateShaderModule(ShadowMethod_GPU_Shader);
        var pipeline        = ctx.CreateComputePipeline(shaderModule, "main", layout);
        
        gpuEffect = new GpuEffect { Layout = layout, Pipeline = pipeline };
        ctx.SetGpuEffect(ShadowMethod_GpuEffectSlot, gpuEffect);
        return gpuEffect;
    }

    // TODO in future the shader should be created at compile time. The binary will be "stored" as generated file (in memory)
    private const string ShadowMethod_GPU_Shader = @"
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
";
    
    // struct for uniforms
    [StructLayout(LayoutKind.Explicit, Size = 16)]  // WGSL uses std140/std430 Layout. Fill up to 16 bytes
    private struct ShadowMethod_Uniforms
    {
        [FieldOffset(0)] public float uniform;
    //  public float uniform2;
    //  public int   iteration;
    }
}