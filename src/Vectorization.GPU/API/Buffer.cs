// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

public readonly ref struct Buffer<T> where T : unmanaged
{
    public  readonly    Span<T>         span;
    public  readonly    GpuBuffer<T>    gpuBuffer;
    public  readonly    int             length;
    public  readonly    int             offset;
    
    private Buffer(Span<T> span) {
        this.span   = span;
        length      = span.Length;
    }
    
    private Buffer(Memory<T> memory) {
        span        = memory.Span;
        length      = memory.Length;
    }
    
    private Buffer(BufferView<T> view) {
        gpuBuffer   = view.gpuBuffer;
        span        = view.Span;
        length      = view.Length;
        offset      = view.Offset;
    }
    
    private Buffer(GpuBuffer<T> gpuBuffer) {
        this.gpuBuffer  = gpuBuffer;
        span            = gpuBuffer.Span;
        length          = gpuBuffer.Length;
    }
    
    public static implicit operator Buffer<T>(T[]           array)      => new(array);
    public static implicit operator Buffer<T>(Span<T>       span)       => new(span);
    public static implicit operator Buffer<T>(Memory<T>     memory)     => new(memory);
    public static implicit operator Buffer<T>(BufferView<T> view)       => new(view);
    public static implicit operator Buffer<T>(GpuBuffer<T>  gpuBuffer)  => new(gpuBuffer);
}

public readonly ref struct InBuffer<T> where T : unmanaged
{
    public  readonly    ReadOnlySpan<T> span;
    public  readonly    GpuBuffer<T>    gpuBuffer;
    public  readonly    int             length;
    public  readonly    int             offset;
    
    private InBuffer(ReadOnlySpan<T> span) {
        this.span   = span;
        length      = span.Length;
    }
    
    private InBuffer(ReadOnlyMemory<T> memory) {
        span        = memory.Span;
        length      = memory.Length;
    }
    
    private InBuffer(ReadOnlyView<T> view) {
        gpuBuffer   = view.gpuBuffer;
        span        = view.Span;
        length      = view.Length;
        offset      = view.Offset;
    }
    
    private InBuffer(GpuBuffer<T> gpuBuffer) {
        this.gpuBuffer  = gpuBuffer;
        span            = gpuBuffer.Span;
        length          = gpuBuffer.Length;
    }
    
    // public static implicit operator ReadOnlyBuffer<T>(T[] array)  new(array); intentionally not available
    
    public static implicit operator InBuffer<T>(ReadOnlySpan<T>   span)       => new(span);
    public static implicit operator InBuffer<T>(ReadOnlyMemory<T> memory)     => new(memory);
    public static implicit operator InBuffer<T>(ReadOnlyView<T>   view)       => new(view);
    public static implicit operator InBuffer<T>(GpuBuffer<T>      gpuBuffer)  => new(gpuBuffer);
}



