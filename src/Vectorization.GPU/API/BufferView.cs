// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
namespace Friflo.Vectorization.GPU;

public readonly struct BufferView<T> where T : unmanaged
{
    public readonly GpuBuffer<T>    GpuBuffer;
    public readonly int             Offset;
    public readonly int             Length;
    
    public          Span<T>         Span =>  GpuBuffer.Span.Slice(Offset, Length);  

    internal BufferView(GpuBuffer<T> gpuBuffer, int offset, int length)
    {
        GpuBuffer   = gpuBuffer;
        Offset      = offset;
        Length      = length;
    }
}

public readonly struct InBufferView<T> where T : unmanaged
{
    public readonly GpuBuffer<T>    GpuBuffer;
    public readonly int             Offset;
    public readonly int             Length;
    
    public  ReadOnlySpan<T>         Span =>  GpuBuffer.Span.Slice(Offset, Length);

    internal InBufferView(GpuBuffer<T> gpuBuffer, int offset, int length)
    {
        GpuBuffer   = gpuBuffer;
        Offset      = offset;
        Length      = length;
    }
}