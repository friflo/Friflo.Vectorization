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
public readonly struct InOutView<T> where T : unmanaged
{
    internal readonly   GpuBuffer<T>    Buffer;
    public   readonly   int             Offset;
    public   readonly   int             Length;
    
    /// <summary>
    /// Gets a <see cref="Span{T}"/> representing the CPU-side host memory slice defined by this view.<br/>
    /// Modifications to this span directly update the host memory, which needs synchronization with the GPU.
    /// </summary>
    public              Span<T>         Span        =>  Buffer.hostMemory.Span.Slice(Offset, Length);

    public   override   string          ToString()  => BufferUtils.ViewToString("InOutView", Buffer, Offset, Length);

    internal InOutView(GpuBuffer<T> buffer, int offset, int length)
    {
        Buffer  = buffer;
        Offset  = offset;
        Length  = length;
    }
    
    /// <summary> Queues the view data for transfer from GPU to host memory. </summary>
    /// <remarks>
    /// Note: Data is not available until <see cref="GpuQueue.ReadBuffers"/> has been 
    /// called and synchronization is complete. This call is non-blocking.
    /// </remarks>
    public InOutView<T> Read() {
        Buffer.Device.Context.QueueRead(Buffer.DeviceBufferId, Offset, Length);
        return this;
    }
    
    /// <summary> Queues the view data for transfer from GPU to host memory. </summary>
    /// <remarks>
    /// Note: Data is not available until <see cref="GpuQueue.ReadBuffers"/> has been 
    /// called and synchronization is complete. This call is non-blocking.
    /// </remarks>
    public InOutView<T> Read(PipelineContext context) {
        context.ValidateThreadSafety();
        context.QueueRead(Buffer.DeviceBufferId, Offset, Length);
        return this;
    }
    
    /// <summary> Queues the view data for transfer from host memory to GPU. </summary>
    /// <remarks>
    /// Note: Data is not immediately available on the GPU.
    /// The transfer is queued when calling a kernel or shader using this view. This call is non-blocking.
    /// </remarks>
    public InOutView<T> Write() {
        Buffer.Device.Context.QueueWrite(Buffer.DeviceBufferId, Offset, Length);
        return this;
    }
    
    /// <summary> Queues the view data for transfer from host memory to GPU. </summary>
    /// <remarks>
    /// Note: Data is not immediately available on the GPU.
    /// The transfer is queued when calling a kernel or shader using this view. This call is non-blocking.
    /// </remarks>
    public InOutView<T> Write(PipelineContext context) {
        context.ValidateThreadSafety();
        context.QueueWrite(Buffer.DeviceBufferId, Offset, Length);
        return this;
    }
}

/// <summary>
/// Provides a read-only window into a sub-region of a <see cref="GpuBuffer{T}"/>.
/// </summary>
public readonly struct InView<T> where T : unmanaged
{
    internal readonly   GpuBuffer<T>    Buffer;
    public   readonly   int             Offset;
    public   readonly   int             Length;
    
    /// <summary>
    /// Gets a <see cref="Span{T}"/> representing the CPU-side host memory slice defined by this view.<br/>
    /// This view provides restricted, read-only access for the GPU.
    /// </summary>
    public              Span<T>         Span        =>  Buffer.hostMemory.Span.Slice(Offset, Length);
    
    public   override   string          ToString()  => BufferUtils.ViewToString("InView", Buffer, Offset, Length);

    internal InView(GpuBuffer<T> buffer, int offset, int length)
    {
        Buffer  = buffer;
        Offset  = offset;
        Length  = length;
    }
    
    /// <summary> Queues the view data for transfer from host memory to GPU. </summary>
    /// <remarks>
    /// Note: Data is not immediately available on the GPU.
    /// The transfer is queued when calling a kernel or shader using this view. This call is non-blocking.
    /// </remarks>
    public InView<T> Write() {
        Buffer.Device.Context.QueueWrite(Buffer.DeviceBufferId, Offset, Length);
        return this;
    }
    
    /// <summary> Queues the view data for transfer from host memory to GPU. </summary>
    /// <remarks>
    /// Note: Data is not immediately available on the GPU.
    /// The transfer is queued when calling a kernel or shader using this view. This call is non-blocking.
    /// </remarks>
    public InView<T> Write(PipelineContext context) {
        context.ValidateThreadSafety();
        context.QueueWrite(Buffer.DeviceBufferId, Offset, Length);
        return this;
    }
}