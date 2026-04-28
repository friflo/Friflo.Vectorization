using System;
using System.Threading.Tasks;

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
    private static unsafe void ShadowMethod_GPU(Buffer<byte> weight, Buffer<float> input, float uniform, GpuEncoder encoder) {
        // ...
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