using System;
using System.Threading.Tasks;

namespace Tests.Generators.Lab;

public static class TestCompute
{
    // generated shadow Method
    public static unsafe GpuTask ShadowMethod(Buffer<byte> weight, Buffer<float> input, float uniform, ExeType exe)
    {
        switch (exe) {
            case ExeType.Scalar:
            case ExeType.SIMD:
                 _ = weight.span; _ = input.gpuBuffer; // use spans in SIMD loop 
                return GpuTask.Completed;
            case ExeType.GPU:
                var ctx = input.gpuBuffer?.Context ?? weight.gpuBuffer?.Context;
                if (ctx == null) throw new InvalidOperationException("GPU execution requested without GpuBuffer<T>");
                // ctx.DispatchMultiply(weight.gpuBuffer, input.gpuBuffer, uniform)
                return new GpuTask(ctx.WgpuPtr, ctx.DevicePtr);
            default: throw new InvalidOperationException();
        }
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
}