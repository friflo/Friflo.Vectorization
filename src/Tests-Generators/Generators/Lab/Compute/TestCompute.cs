using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Silk.NET.WebGPU;

// ReSharper disable InconsistentNaming
namespace Tests.Generators.Lab;

public static class TestCompute
{
    // ------------------------ generated code: begin
    // generated shadow Method
    public static GpuBuffer<float> ShadowMethod(
        Buffer<byte>    weight,
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
    private static unsafe void ShadowMethod_AVX(Buffer<byte> weight, Buffer<float> input, float uniform, Buffer<float> output) {
        // ...
    }
    
    // generated GPU method
    // Notes:
    // - in case this method throws an exception before finishing the pass the task is cleared at next Reset() - no WebGPU leaks.
    // - method does not need to know how to Finish() an encoder. It asks for Encoder and fills it.
    private static GpuBuffer<float> ShadowMethod_GPU(Buffer<byte> weight, Buffer<float> input, float uniform, Buffer<float> output)
    {
        var ctx         = input.gpuBuffer?.Context ?? weight.gpuBuffer?.Context ?? throw new Exception();
        GpuTask task    = ctx.RentTask();
        var gpuOutput   = output.gpuBuffer ?? ctx.RentBuffer<float>(input.Length);
        try {
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
                var bindGroup = ctx.CreateBindGroup(gpuEffect.Layout, [
                    GpuBindEntry.From (0, weight.gpuBuffer),
                    GpuBindEntry.From (1, input.gpuBuffer),
                    ctx.AsUniformEntry(2, uniforms),
                    GpuBindEntry.From (3, output.gpuBuffer),
                ]);
                pass.SetBindGroup(0, bindGroup);
                pass.DispatchWorkgroups(input.Length / 64, 1, 1);               // Execute ComputePass
                pass.End();                                                     // finish Pass (required by WebGPU State-Machine)
            }
            // connect task to output
            gpuOutput.LastWritingTask = task;
            ctx.Enqueue(task);
        } catch {
            ctx.ReturnTask(task);
        }
        gpuOutput.WaitInDebug();
        return gpuOutput;
    }
    
    private static readonly int ShadowMethod_GpuEffectSlot = GpuBindGroupLayout.NewGpuEffectSlot(); 
    
    private static GpuEffect ShadowMethod_GPU_GetGpuEffect(GpuContext ctx)
    {
        var gpuEffect = ctx.GetGpuEffect(ShadowMethod_GpuEffectSlot); // array index lookup
        var layout = gpuEffect.Layout;
        if (layout != null) {
            return gpuEffect;
        }
        layout = ctx.BindGroupLayoutBuilder()
            .AddBuffer<byte>  (0, "weight")     // @binding(0) var<storage, read>       weight
            .AddBuffer<float> (1, "input")      // @binding(1) var<storage, read>       input
            .AddUniform<float>(2, "uniform")    // @binding(2) var<uniform>             uniforms
            .AddBuffer<float> (3, "output")     // @binding(3) var<storage, read_write> output
            .Build();
        
        var shaderModule = ctx.CreateShaderModule(ShadowMethod_GPU_Shader);
        var pipeline = ctx.CreateComputePipeline(shaderModule, "main", layout);
        
        gpuEffect = new GpuEffect { Layout = layout, Pipeline = pipeline };
        ctx.SetGpuEffect(ShadowMethod_GpuEffectSlot, gpuEffect);
        return gpuEffect;
    }

    // TODO in future the shader should be created at compile time. The binary will be "stored" as generated file (in memory)
    private const string ShadowMethod_GPU_Shader = @"
struct ShadowMethod_Uniforms {
    uniform : f32,
};

@group(0) @binding(0) var<storage, read>        weight:     array<u32>; // byte mapping
@group(0) @binding(1) var<storage, read>        input:      array<f32>;
@group(0) @binding(2) var<uniform>              uniforms:   ShadowMethod_Uniforms;
@group(0) @binding(3) var<storage, read_write>  output:     array<f32>;

@compute @workgroup_size(64)
fn main(@builtin(global_invocation_id) global_id: vec3<u32>) {
    let index = global_id.x;
    
    let weight_scalar = f32(weight[index]); // cast u32 -> f32
    // shader body generated from Blueprint method body
    output[index] = (input[index] * weight_scalar) + uniforms.uniform;
}
";
    
    // struct for uniforms
    [StructLayout(LayoutKind.Sequential)]
    private struct ShadowMethod_Uniforms
    {
        public float uniform;
        private float _pad0, _pad1, _pad2; // WGSL uses std140/std430 Layout. Fill up to 16 bytes
    //  public float uniform2;
    //  public int   iteration;
    }
    // ------------------------ generated code: end

    
    
    private static void UseSpan<T>(Span<T> span) { }
    
    public static async Task ExampleCompute()
    {
        var weight  = new Span<byte> (new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
        var input   = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var output  = new float[9];
        
        UseSpan(weight);
        
        var result1 = ShadowMethod(weight, input, 42, ExeType.SIMD, output);
        // result1 - no Wait() on result1. Nothing will happen - user is surprised :)
        
    //  UseSpan(weight); // compiler error
        
        using var gpuContext = new GpuContext();
        var gpuWeight = new GpuBuffer<byte> (gpuContext, 100, BufferUsage.None);
        var gpuInput  = new GpuBuffer<float>(gpuContext, 100, BufferUsage.None);
        var output2   = new GpuBuffer<float>(gpuContext, 100, BufferUsage.None);
        var result2 = ShadowMethod(gpuWeight, gpuInput, 42, ExeType.SIMD, output2);
        gpuContext.Wait(result2);
    }
    
    public class ModelLayer {
        public GpuBuffer<byte>     weight;
        public GpuBuffer<float>    input;
        public GpuBuffer<float>    output;
    }
    
    public static void RunInference(ModelLayer[] layers, GpuContext ctx)
    {
        // Fire Layer 1 to 50
        GpuBuffer<float> result = null;
        foreach (var layer in layers) {
            result = ShadowMethod(layer.weight, layer.input, 42, ExeType.GPU, layer.output);
        }
        // Wait only on lastTask. Very efficient. GpuTask works intern with DevicePoll()
        ctx.Wait(result);
    }
    

    // --- compact examples of some generated shadow method stubs
    public static GpuBuffer<float> ComputeLayer1(Buffer<byte> weight, Buffer<float> input, ExeType exe) { return null; }
    public static GpuBuffer<float> ComputeLayer2(Buffer<float> input, ExeType exe) { return null; }
    
    public static GpuBuffer<byte> InitWeights(GpuContext context) {
        return null;
    }
    

    public static void DependencyFlow(Buffer<float> input)
    {
        using var context = new GpuContext();
        var weight = InitWeights(context);
        var a = ComputeLayer1(weight, input, ExeType.GPU);
    //  firstValue = a[0];                              // TODO indexer must ctx.Wait(this) - than returns firstValue
        var b = ComputeLayer2(a, ExeType.GPU);
        context.Wait(b);
    }
    
}