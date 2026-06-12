// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU.Runtime;

[assembly: CLSCompliant(true)]

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

[CLSCompliant(true)]
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class GpuBuffer : IDisposable
{
    public  readonly    string      Label;
    public  readonly    int         Length;
    public	readonly    long        Id              = BufferUtils.NextId();
    public	abstract    GpuDevice   Device          { get; }
    
    [EditorBrowsable(EditorBrowsableState.Never)] [CLSCompliant(false)]
    public	readonly    uint        DeviceBufferId;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public  readonly    nint        NativeHandle;
    
    public  override    string      ToString()      => $"{Label}({Id}): {(IsDisposed ? "Disposed" : "Alive")}";
    
    // --- abstract
    public  abstract    bool        IsDisposed { get; }
    public  abstract    void        Dispose();

    protected GpuBuffer(int length, string label, nint nativeHandle, int bufferId)
    {
        Label           = label;
        Length          = length;
        NativeHandle    = nativeHandle;
        DeviceBufferId  = (uint)bufferId;
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)][StackTraceHidden][DoesNotReturn]
    protected IndexOutOfRangeException OutOfRangeException(int start, int length) {
        return new IndexOutOfRangeException($"Range: [{start}, {start + length}]  Length: {Length}  Buffer: {Label}");
    }
}

public interface IReadOnlyGpuBuffer<T> : IDisposable where T : unmanaged
{
    public InView<T>    In();
    public InView<T>    In(int start, int length);
}


/// <summary>
/// Represents the base class for GPU-mapped memory buffers.<br/>
/// It contains a CPU host memory and supports synchronization of the memory with the GPU.<br/>
/// It provides methods returning <see cref="InOutView{T}"/> and <see cref="InView{T}"/> to for safe host memory access. 
/// </summary>
public abstract class GpuBuffer<T> :
    GpuBuffer,              // enables non-generic access to fields like: Length, Device, ... 
    IReadOnlyGpuBuffer<T>   // enables read only access to immutable buffer data
        where T : unmanaged
{
    /// <summary> The CPU-accessible host memory used as a staging area for GPU synchronization. </summary>
    protected internal readonly  Memory<T>    hostMemory;
    
    protected GpuBuffer(Memory<T> hostMemory, string label, nint nativeHandle, int bufferId)
        :  base(hostMemory.Length, label, nativeHandle, bufferId)
    {
        this.hostMemory = hostMemory;
    }

    /// <summary> Creates a read-write view of the entire buffer. </summary>
    public InOutView<T> InOut() => new(this, 0, Length);

    /// <summary> Creates a read-write view of a sub-region within this buffer. </summary>
    public InOutView<T> InOut(int start, int length)
    {
        if (start >= 0 && length >= 0 && start + length <= Length)
            return new InOutView<T>(this, start, length);
        throw OutOfRangeException(start, length);
    }
    
    /// <summary> Gets a read-only view of the entire buffer. </summary>
    public InView<T>    In() => new(this, 0, Length);

    /// <summary> Creates a read-only view of a sub-region within this buffer. </summary>
    public InView<T>    In(int start, int length)
    {
        if (start >= 0 && length >= 0 && start + length <= Length)
            return new InView<T>(this, start, length);
        throw OutOfRangeException(start, length);
    }
}

/// <summary>
/// Defines allowed data flow of <see cref="GpuBuffer{T}"/> data from CPU to GPU.<br/>
/// </summary>
public enum BufferProfile
{
    /// <summary> CPU can write/read data to/from GPU. </summary>           <remarks>BufferUsage: CopyDst | CopySrc</remarks>
    InOut       = 0,
    
    /// <summary> CPU writes data once to GPU. </summary>                   <remarks>BufferUsage: CopyDst</remarks>
    StaticIn    = 1,
    
    /// <summary> CPU can only read compute results from GPU. </summary>    <remarks>BufferUsage: CopySrc</remarks>
    PureOut     = 2
}

/// <summary>
/// Defines the hardware-specific role and optimization path of the buffer inside the GPU pipeline.
/// </summary>
public enum BufferType
{
    /// <summary> Large data arrays accessible by compute and graphics shaders. </summary>
    Storage     = 0,
    
    /// <summary> Small, fast, read-only configuration data for shaders. </summary>
    Uniform     = 1,
    
    /// <summary> Fed directly into the Vertex Input Assembly stage to draw geometry. </summary>
    Vertex      = 2,
    
    /// <summary> Fed directly into the Index Input Assembly stage to lookup vertices. </summary>
    Index       = 3,
    
    /// <summary> Contains execution arguments for indirect dispatch or draw calls. </summary>
    Indirect    = 4
}

