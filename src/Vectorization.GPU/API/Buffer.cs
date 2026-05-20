// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

public ref struct Buffer<T> where T : unmanaged
{
    public              Span<T>         span;
    public  readonly    GpuBuffer<T>    gpuBuffer;
    
    public 	            int 			Count => gpuBuffer?.Length ?? span.Length;
    
    private Buffer(Span<T> span) {
        this.span = span;
    }
    
    private Buffer(Memory<T> memory) {
        span = memory.Span;
    }
    
    private Buffer(GpuBuffer<T> gpuBuffer) {
        this.gpuBuffer  = gpuBuffer;
        span            = gpuBuffer.Span;
    }
    
    public static implicit operator Buffer<T>(T[]           array)      => new(array);
    public static implicit operator Buffer<T>(Span<T>       span)       => new(span);
    public static implicit operator Buffer<T>(Memory<T>     memory)     => new(memory);
    public static implicit operator Buffer<T>(GpuBuffer<T>  gpuBuffer)  => new(gpuBuffer);
}

public ref struct InBuffer<T> where T : unmanaged
{
    public              ReadOnlySpan<T> span;
    public  readonly    GpuBuffer<T>    gpuBuffer;
    
    public 	            int 			Count => gpuBuffer?.Length ?? span.Length;
    
    private InBuffer(ReadOnlySpan<T> span) {
        this.span = span;
    }
    
    private InBuffer(ReadOnlyMemory<T> memory) {
        span = memory.Span;
    }
    
    private InBuffer(GpuBuffer<T> gpuBuffer) {
        this.gpuBuffer  = gpuBuffer;
        span            = gpuBuffer.Span;
    }
    
    // public static implicit operator ReadOnlyBuffer<T>(T[] array)  new(array); intentionally not available
    
    public static implicit operator InBuffer<T>(ReadOnlySpan<T>   span)       => new(span);
    public static implicit operator InBuffer<T>(ReadOnlyMemory<T> memory)     => new(memory);
    public static implicit operator InBuffer<T>(GpuBuffer<T>      gpuBuffer)  => new(gpuBuffer);
}



