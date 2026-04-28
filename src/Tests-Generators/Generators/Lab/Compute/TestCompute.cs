using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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
                pass.SetPipeline(ctx.GetPipeline("MyShader"));                  // Set Pipeline to "MyShader"
                
                var layout    = ShadowMethod_GPU_GetBindGroupLayout(ctx);
                var uniforms  = new ShadowMethod_Uniforms { uniform = uniform };
                var bindGroup = ctx.CreateBindGroup(layout, [
                    GpuBindEntry.From (0, weight.gpuBuffer),
                    GpuBindEntry.From (1, input.gpuBuffer),
                    ctx.AsUniformEntry(2, uniforms)
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
    
    private static readonly int ShadowMethod_BindGroupLayoutSlot = GpuBindGroupLayout.NewBindGroupLayoutSlot(); 
    
    private static GpuBindGroupLayout ShadowMethod_GPU_GetBindGroupLayout(GpuContext ctx)
    {
        GpuBindGroupLayout layout = ctx.GetBindGroupLayout(ShadowMethod_BindGroupLayoutSlot); // array index lookup
        if (layout != null) {
            return layout;
        }
        layout = ctx.BindGroupLayoutBuilder()
            .AddBuffer<byte>  (0)  // @group(0) @binding(0) var<storage, read> weight: array<u8>;
            .AddBuffer<float> (1)  // @group(0) @binding(1) var<storage, read> input: array<f32>;
            .AddUniform<float>(2)  // @group(0) @binding(2) var<uniform> myParam: f32;        <--- we aim for this
            .Build();
        ctx.SetBindGroupLayout(ShadowMethod_BindGroupLayoutSlot, layout);
        return layout;
    }
    
    // Vom Source Generator erzeugtes Struct für die Uniforms
    [StructLayout(LayoutKind.Sequential)]
    private struct ShadowMethod_Uniforms
    {
        public float uniform;
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
        var gpuWeight = new GpuBuffer<byte>(gpuContext, 100);
        var gpuInput  = new GpuBuffer<float>(gpuContext, 100);
        var output2   = new GpuBuffer<float>(gpuContext, 100);
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