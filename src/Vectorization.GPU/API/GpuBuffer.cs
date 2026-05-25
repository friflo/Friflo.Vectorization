// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class GpuBuffer : IDisposable
{
    public    readonly  string      Label;
    public    readonly  int         Length;
    public	  readonly  long        Id              = GpuBufferUtils.NextId();
    public	  readonly  int         DeviceBufferId;
    public	  abstract  GpuDevice   Device          { get; }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public	  readonly  nint        NativeHandle;
    
    public    override  string      ToString()      => $"{Label}({Id}): {(IsDisposed ? "Disposed" : "Alive")}";
    
    // --- abstract
    public  abstract    bool        IsDisposed { get; }
    public  abstract    void        Dispose();

    protected GpuBuffer(int length, string label, nint nativeHandle, int bufferId)
    {
        Label           = label;
        Length          = length;
        NativeHandle    = nativeHandle;
        DeviceBufferId  = bufferId;
    }
}

public interface IReadOnlyGpuBuffer<T> : IDisposable where T : unmanaged
{
    public ReadOnlyView<T>  In { get; }
    public ReadOnlyView<T>  AsReadOnly(int start, int length);
}


/// <summary>
/// Represents the base class for GPU-mapped memory buffers.<br/>
/// It contains a CPU host memory and supports synchronization of the memory with the GPU.<br/>
/// It provides methods returning <see cref="BufferView{T}"/> and <see cref="ReadOnlyView{T}"/> to access the host memory. 
/// </summary>
public abstract class GpuBuffer<T> :    // enable raw access to buffer data without any safety guards
    GpuBuffer,                          // enables non-generic access to fields like: Length, Device, ... 
    IReadOnlyGpuBuffer<T>,              // enables read only access to immutable buffer data
    IScopedGpuBuffer<T>                 // enables read / write of buffer data without race conditions
        where T : unmanaged
{
    /// <summary> The CPU-accessible host memory used as a staging area for GPU synchronization. </summary>
    protected internal readonly  Memory<T>    hostMemory;
    
    protected GpuBuffer(Memory<T> hostMemory, string label, nint nativeHandle, int bufferId)
        :  base(hostMemory.Length, label, nativeHandle, bufferId)
    {
        this.hostMemory = hostMemory;
    }
    
    /// <summary> Gets the raw CPU-side backing memory for this buffer. </summary>
    /// <remarks>
    /// <b>Synchronization Notice:</b> This memory is not automatically synchronized with the GPU.
    /// <list type="bullet">
    /// <item> <b>Concurrency:</b><br/>
    ///   CPU and GPU must not access this memory simultaneously to avoid data races.
    ///   Ensure the GPU has finished all pending work before modifying this memory.
    /// </item>
    /// <item> <b>Explicit Sync:</b><br/>
    ///   Modifications to this memory are only reflected on the GPU after calling <c>Upload()</c>.
    ///   GPU updates are only visible in this memory after calling <c>Download()</c>.
    /// </item>
    /// </list>
    /// </remarks>
    public BufferView<T>    InOut   => new (this, 0, Length);
    /// <summary> Gets a read-only view of the entire buffer. </summary>
    public ReadOnlyView<T>  In      => new (this, 0, Length);
    
    /// <summary> Creates a read-write view of a sub-region within this buffer. </summary>
    public BufferView<T>    Slice     (int start, int length)
    {
        if (start >= 0 && length >= 0 && start + length <= Length)
            return new BufferView<T>(this, start, length);
        throw OutOfRangeException(start, length);
    }

    /// <summary> Creates a read-only view of a sub-region within this buffer. </summary>
    public ReadOnlyView<T>  AsReadOnly(int start, int length)
    {
        if (start >= 0 && length >= 0 && start + length <= Length)
            return new ReadOnlyView<T>(this, start, length);
        throw OutOfRangeException(start, length);
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)][StackTraceHidden][DoesNotReturn]
    private IndexOutOfRangeException OutOfRangeException(int start, int length) {
        return new IndexOutOfRangeException($"Range: [{start}, {start + length}]  Length: {Length}  Buffer: {Label}");
    }


    public BufferWriter<T>  GetWriter() {
        // optional check if buffer is ready for write access. E.g. fence state
        return new BufferWriter<T>(this, hostMemory.Span);
    }

    public BufferReader<T>  GetReader() {
        // this.Download();
        return new BufferReader<T>(this, hostMemory.Span);
    }

    public T this[int index]
    {
        get {
            /* if (LastWritingTask != null && !LastWritingTask.IsCompleted) { TASK_TAG
                Device.Wait(this); // force Compute before CPU reads value
            }*/
            throw new NotImplementedException();
            // return InternalDownloadValue(index);
        }
    }
    
    public  abstract    void    Download(GpuBuffer<T> gpuBuffer, T[] targetArray);
}



[Flags]
public enum GpuBufferUsage
{
    None            = 0x0,
    MapRead         = 0x1,
    MapWrite        = 0x2,
    CopySrc         = 0x4,
    CopyDst         = 0x8,
    Index           = 0x10,
    Vertex          = 0x20,
    Uniform         = 0x40,
    Storage         = 0x80,
    Indirect        = 0x100,
    QueryResolve    = 0x200,
//  Force32         = 0x7FFFFFFF,
}

internal static class GpuBufferUtils
{
    private static long IdCounter;
    
    internal static long NextId() => Interlocked.Increment(ref IdCounter);
}

