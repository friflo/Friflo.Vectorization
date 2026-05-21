// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
namespace Friflo.Vectorization.GPU;

public readonly struct BufferView<T> where T : unmanaged
{
    internal readonly   GpuBuffer<T>    gpuBuffer;
    public   readonly   int             Offset;
    public   readonly   int             Length;
    
    public              Span<T>         Span =>  gpuBuffer.HostMemory.Span.Slice(Offset, Length);

    internal BufferView(GpuBuffer<T> gpuBuffer, int offset, int length)
    {
        this.gpuBuffer  = gpuBuffer;
        Offset          = offset;
        Length          = length;
    }
}

public readonly struct ReadOnlyView<T> where T : unmanaged
{
    internal readonly   GpuBuffer<T>    gpuBuffer;
    public   readonly   int             Offset;
    public   readonly   int             Length;
    
    public              ReadOnlySpan<T> Span =>  gpuBuffer.HostMemory.Span.Slice(Offset, Length);

    internal ReadOnlyView(GpuBuffer<T> gpuBuffer, int offset, int length)
    {
        this.gpuBuffer  = gpuBuffer;
        Offset          = offset;
        Length          = length;
    }
}