// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization.GPU.Runtime;

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
namespace Friflo.Vectorization.GPU;

/// <summary>
/// Provides a read-write window into a sub-region of a <see cref="GpuBuffer{T}"/>.
/// </summary>
public readonly struct BufferView<T> where T : unmanaged
{
    internal readonly   GpuBuffer<T>    gpuBuffer;
    public   readonly   int             Offset;
    public   readonly   int             Length;
    
    /// <summary>
    /// Gets a <see cref="Span{T}"/> representing the CPU-side host memory slice defined by this view.<br/>
    /// Modifications to this span directly update the host memory, which needs synchronization with the GPU.
    /// </summary>
    public              Span<T>         Span        =>  gpuBuffer.hostMemory.Span.Slice(Offset, Length);

    public   override   string          ToString()  => BufferUtils.ViewToString("BufferView", gpuBuffer, Offset, Length);

    internal BufferView(GpuBuffer<T> gpuBuffer, int offset, int length)
    {
        this.gpuBuffer  = gpuBuffer;
        Offset          = offset;
        Length          = length;
    }
}

/// <summary>
/// Provides a read-only window into a sub-region of a <see cref="GpuBuffer{T}"/>.
/// </summary>
public readonly struct ReadOnlyView<T> where T : unmanaged
{
    internal readonly   GpuBuffer<T>    gpuBuffer;
    public   readonly   int             Offset;
    public   readonly   int             Length;
    
    /// <summary>
    /// Gets a <see cref="ReadOnlySpan{T}"/> representing the CPU-side host memory slice defined by this view.<br/>
    /// This view provides restricted, read-only access to the mapped host memory.
    /// </summary>
    public              ReadOnlySpan<T> Span        =>  gpuBuffer.hostMemory.Span.Slice(Offset, Length);
    
    public   override   string          ToString()  => BufferUtils.ViewToString("ReadOnlyView", gpuBuffer, Offset, Length);

    internal ReadOnlyView(GpuBuffer<T> gpuBuffer, int offset, int length)
    {
        this.gpuBuffer  = gpuBuffer;
        Offset          = offset;
        Length          = length;
    }
}