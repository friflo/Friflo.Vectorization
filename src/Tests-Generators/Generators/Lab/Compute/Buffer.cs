using System;

// ReSharper disable InconsistentNaming
namespace Tests.Generators.Lab;

public enum ExeType {
    Scalar,
    SIMD,
    GPU
}

public ref struct Buffer<T> where T : struct
{
    public  Span<T>         span;
    public  GpuBuffer<T>    gpuBuffer;
    
    public  GpuTask         LastWritingTask { get => gpuBuffer.LastWritingTask; set => gpuBuffer.LastWritingTask = value; }

    
    public int Length => gpuBuffer?.Length ?? span.Length;
    
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

