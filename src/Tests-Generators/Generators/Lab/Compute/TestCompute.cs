using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
namespace Tests.Generators.Lab;

public static class TestCompute
{
    // generated shadow Method
    public static GpuTask ShadowMethod(Buffer<byte> weight, Buffer<float> input, float uniform, ExeType exe, GpuBatch batch = null)
    {
        if (exe == ExeType.GPU) {
            var ctx = input.gpuBuffer?.Context ?? weight.gpuBuffer?.Context ?? throw new Exception();
            // record Dispatch (in Batch oder temporary Encoder)
            if (batch != null) {
                // Batch Mode
                ShadowMethod_GPU(weight.gpuBuffer, input.gpuBuffer, uniform, batch.Encoder);
                return GpuTask.Completed;
            }
            // Immediate Mode
            using GpuEncoder encoder = ctx.CreateEncoder();
            ctx.Submit(encoder.Finish());
            return new GpuTask(ctx);
        }
        // Scalar / SIMD
        ShadowMethod_AVX(weight, input, uniform);
        return GpuTask.Completed;
    }
    
    // generated AVX method
    private static unsafe void ShadowMethod_AVX(Buffer<byte> weight, Buffer<float> input, float uniform) {
        // ...
    }
    
    // generated GPU method
    private static unsafe void ShadowMethod_GPU(Buffer<byte> weight, Buffer<float> input, float uniform, GpuEncoder encoder)
    {
        var ctx = encoder.context;
        using GpuComputePass pass = encoder.BeginComputePass();         // Start ComputePass
        pass.SetPipeline(ctx.GetPipeline("MyShader"));                  // Set Pipeline to "MyShader"
        
        GpuBindGroupLayout layout = ShadowMethod_GPU_GetBindGroupLayout(ctx);
        var uniforms = new ShadowMethod_Uniforms { uniform = uniform };
        var bindGroup = ctx.CreateBindGroup(layout, [
            new GpuBindEntry  (0, weight.gpuBuffer),
            new GpuBindEntry  (1, input.gpuBuffer),
            ctx.AsUniformEntry(2, uniforms)
        ]);
        pass.SetBindGroup(0, bindGroup);
        
        pass.DispatchWorkgroups(input.Length / 64, 1, 1);               // Execute ComputePass
        pass.End();                                                     // finish Pass (required by WebGPU State-Machine)
    }
    
    private static readonly int ShadowMethod_BindGroupLayoutSlot = GpuBindGroupLayout.NewBindGroupLayoutSlot(); 
    
    private static GpuBindGroupLayout ShadowMethod_GPU_GetBindGroupLayout(GpuContext ctx)
    {
        GpuBindGroupLayout layout = ctx.GetBindGroupLayout(ShadowMethod_BindGroupLayoutSlot);
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
    public struct ShadowMethod_Uniforms
    {
        public float uniform;
    //  public float uniform2;
    //  public int   iteration;
    }
    
    private static void UseSpan<T>(Span<T> span) { }
    
    public static async Task ExampleCompute()
    {
        var weight  = new Span<byte> (new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
        var input   = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        
        UseSpan(weight);
        
        var task1 = ShadowMethod(weight, input, 42, ExeType.SIMD);
        await task1.Completion();
        
    //  UseSpan(weight); // compiler error
        
        using var gpuContext = new GpuContext();
        var gpuWeight = new GpuBuffer<byte>(gpuContext, 100);
        var gpuInput = new GpuBuffer<float>(gpuContext, 100);
        var task2 = ShadowMethod(gpuWeight, gpuInput, 42, ExeType.SIMD);
        
        await task2.Completion();
    }
    
    public class ModelLayer {
        public GpuBuffer<byte>     weight;
        public GpuBuffer<float>    input;
    }
    
    public static async Task RunInference(ModelLayer[] layers)
    {
        // Fire Layer 1 to 50
        var lastTask = GpuTask.Completed;
        foreach (var layer in layers) {
            lastTask = ShadowMethod(layer.weight, layer.input, 42, ExeType.GPU);
        }
        
        // Wait only on lastTask. Very efficient. GpuTask works intern with DevicePoll()
        await lastTask.Completion();
    }
    
    public static async Task RunInferenceCommandRecorder(ModelLayer[] layers)
    {
        using var gpuContext = new GpuContext();
        using var batch = gpuContext.BeginBatch();

        foreach (var layer in layers) {
            // no task is submitted - only recorded
            ShadowMethod(layer.weight, layer.input, 42, ExeType.GPU, batch);
        }
        // submit all recorded tasks added to the batch
        GpuTask totalWork = batch.Submit();
        await totalWork.Completion();
    }
    
}