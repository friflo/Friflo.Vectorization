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
    
    public Buffer(Span<T> span) {
        this.span = span;
    }
    
    public Buffer(Memory<T> memory) {
        span = memory.Span;
    }
    
    public Buffer(GpuBuffer<T> gpuBuffer) {
        this.gpuBuffer  = gpuBuffer;
        span            = gpuBuffer.Span;
    }
    
    public static implicit operator Buffer<T>(T[]           array)      => new(array);
    public static implicit operator Buffer<T>(Span<T>       span)       => new(span);
    public static implicit operator Buffer<T>(Memory<T>     memory)     => new(memory);
    public static implicit operator Buffer<T>(GpuBuffer<T>  gpuBuffer)  => new(gpuBuffer);
}

