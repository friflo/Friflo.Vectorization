// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

public readonly ref struct Buffer<T> where T : unmanaged
{
    public  readonly    Span<T>         Span;
    public  readonly    GpuBuffer<T>    GpuBuffer;
    public  readonly    int             Length;
    public  readonly    int             Offset;
    
    private Buffer(Span<T> span) {
        Span        = span;
        Length      = span.Length;
    }
    
    private Buffer(Memory<T> memory) {
        Span        = memory.Span;
        Length      = memory.Length;
    }
    
    private Buffer(BufferView<T> view) {
        GpuBuffer   = view.gpuBuffer;
        Span        = view.Span;
        Offset      = view.Offset;
        Length      = view.Length;
    }
    
    private Buffer(GpuBuffer<T> gpuBuffer) {
        GpuBuffer   = gpuBuffer;
        Span        = gpuBuffer.HostMemory.Span;
        Length      = gpuBuffer.Length;
    }
    
    // --- CPU buffers
    public static implicit operator Buffer<T>(T[]           array)      => new(array);
    public static implicit operator Buffer<T>(Span<T>       span)       => new(span);
    public static implicit operator Buffer<T>(Memory<T>     memory)     => new(memory);
    // --- GPU buffers
    public static implicit operator Buffer<T>(BufferView<T> view)       => new(view);
    public static implicit operator Buffer<T>(GpuBuffer<T>  gpuBuffer)  => new(gpuBuffer);
}

public readonly ref struct InBuffer<T> where T : unmanaged
{
    public  readonly    ReadOnlySpan<T> Span;
    public  readonly    GpuBuffer<T>    GpuBuffer;
    public  readonly    int             Length;
    public  readonly    int             Offset;
    
    private InBuffer(ReadOnlySpan<T> span) {
        Span        = span;
        Length      = span.Length;
    }
    
    private InBuffer(ReadOnlyMemory<T> memory) {
        Span        = memory.Span;
        Length      = memory.Length;
    }
    
    private InBuffer(ReadOnlyView<T> view) {
        GpuBuffer   = view.gpuBuffer;
        Span        = view.Span;
        Offset      = view.Offset;
        Length      = view.Length;
    }
    
    private InBuffer(GpuBuffer<T> gpuBuffer) {
        GpuBuffer   = gpuBuffer;
        Span        = gpuBuffer.HostMemory.Span;
        Length      = gpuBuffer.Length;
    }
    
    // public static implicit operator ReadOnlyBuffer<T>(T[] array)  new(array); intentionally not available
    
    // --- CPU buffers
    public static implicit operator InBuffer<T>(ReadOnlySpan<T>   span)       => new(span);
    public static implicit operator InBuffer<T>(ReadOnlyMemory<T> memory)     => new(memory);
    // --- GPU buffers
    public static implicit operator InBuffer<T>(ReadOnlyView<T>   view)       => new(view);
    public static implicit operator InBuffer<T>(GpuBuffer<T>      gpuBuffer)  => new(gpuBuffer);
}



