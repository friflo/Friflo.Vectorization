using System;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

// ReSharper disable InconsistentNaming
namespace Tests.Generators.Lab;

public enum ExeType {
    Scalar,
    SIMD,
    GPU
}

public unsafe class GpuContext : IDisposable
{
    public Wgpu*    WgpuPtr { get; }
    public Device*  DevicePtr { get; }
    public Queue*   QueuePtr { get; }

    public GpuContext() { }

    public void Dispatch(Buffer<byte> w, Buffer<float> i, float u) 
    {
        // feed CommandEncoder
    }

    public void Dispose() { /* Cleanup native resources */ }
}

public class GpuBuffer<T> {
    public readonly GpuContext Context;  // Creator of GpuBuffer
//  public readonly unsafe Buffer* Ptr;

    public GpuBuffer(GpuContext ctx, uint size) 
    {
        Context = ctx;
        // Ptr = ctx.CreateBuffer(size); ...
    }
}

public ref struct Buffer<T> where T : struct
{
    public Span<T>      span;
    public GpuBuffer<T> gpuBuffer;
    
    public Buffer(Span<T> span) {
        this.span = span;
    }
    public Buffer(GpuBuffer<T> gpuBuffer) {
        this.gpuBuffer = gpuBuffer;
    }
    
    public static implicit operator Buffer<T>(T[] array)    => new(array);
    public static implicit operator Buffer<T>(Span<T> span) => new(span);
    public static implicit operator Buffer<T>(GpuBuffer<T> gpuBuffer) => new(gpuBuffer);
}

public static class TestLab
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
    
    public static void Test() {
        var weight = new Span<byte> (new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
        var input = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        ShadowMethod(weight, input, 42, ExeType.Scalar);
        
        var gpuContext = new GpuContext();
        var gpuWeight = new GpuBuffer<byte>(gpuContext, 100);
        var gpuInput = new GpuBuffer<float>(gpuContext, 100);
        ShadowMethod(gpuWeight, gpuInput, 42, ExeType.Scalar);
    }
}
