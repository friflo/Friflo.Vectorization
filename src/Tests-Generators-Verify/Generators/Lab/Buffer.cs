using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.ObjectPool;

// ReSharper disable InconsistentNaming
namespace Tests.Generators.Lab;

public enum ExeType {
    Scalar,
    SIMD,
    GPU
}

public class GpuBuffer<T> {
    // GPU field/state
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
    public static void ShadowMethod(Buffer<byte> weight, Buffer<float> input, float uniform, ExeType exe)
    {
        switch (exe) {
            case ExeType.Scalar:
                _ = weight.span; _ = input.gpuBuffer; // use spans in scalar loop
                break;
            case ExeType.SIMD:
                 _ = weight.span; _ = input.gpuBuffer; // use spans in SIMD loop 
                break;
            case ExeType.GPU:
                _ = weight.gpuBuffer; _ = input.gpuBuffer; // use gpuBuffer's for GPU
                break;
        }
        
    }
    
    public static void Test() {
        var weight = new Span<byte> (new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
        var input = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        ShadowMethod(weight, input, 42, ExeType.Scalar);
        
        var gpuWeight = new GpuBuffer<byte>();
        var gpuInput = new GpuBuffer<float>();
        ShadowMethod(gpuWeight, gpuInput, 42, ExeType.Scalar);
    }
}
