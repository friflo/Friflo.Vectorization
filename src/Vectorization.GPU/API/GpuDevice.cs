// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.GPU;

public abstract partial class GpuDevice : CommandStream, IDisposable
{
    public    readonly  string          Label;
    public    readonly  int             UniformBufferSize;
    public    readonly  GpuQueue        Queue;
    protected readonly  int             threadId;
    public              PipelineContext Context     => threadContexts.Value;
    
    public  override    string  ToString() => Label + (IsDisposed ? ": Disposed" : ": Alive");

    protected GpuDevice(string label, int uniformBufferSize) {
        Label               = label;
        UniformBufferSize   = uniformBufferSize;
        Queue               = new GpuQueue(this);
        threadId            = Environment.CurrentManagedThreadId;
    }
    
    public IReadOnlyGpuBuffer<T> CreateReadOnlyBuffer<T>    (T[] data, string label, BufferType type = BufferType.Storage) where T : unmanaged {
        return CreateBuffer(data, label, BufferProfile.StaticIn, type);
    }
    
    public PipelineContext          BeginContext([CallerFilePath] string file = "", [CallerLineNumber] int line = 0) => BeginContextInternal(file, line);
    

    public GpuBuffer<T> CreateBuffer<T>(T[] data, string label, BufferProfile profile, BufferType type = BufferType.Storage) where T : unmanaged {
        return CreateBuffer(new Memory<T>(data), label, profile, type);
    }

    public GpuBuffer<T> CreateBuffer<T>(int length, T value, string label, BufferProfile profile, BufferType type = BufferType.Storage) where T : unmanaged {
        var array = new T[length];
        Array.Fill(array, value);
        return CreateBuffer(new Memory<T>(array), label, profile, type);
    }
    
    // --- abstract
    public abstract ComputeMode     DefaultComputeMode  { get; }
    public abstract bool            IsDisposed          { get; }
    public abstract GpuLimits       GetDeviceLimits();
    public abstract GpuBuffer<T>    CreateBuffer<T>(Memory<T> data, string label, BufferProfile profile, BufferType type = BufferType.Storage) where T : unmanaged;
}


/// <summary>  Defines the execution strategy for compute operations. </summary>
/// <remarks>
/// The <see cref="ComputeMode"/> determines which backend (CPU or GPU)  is used to perform calculations.<br/>
/// When <see cref="Device"/> is selected, the <see cref="GpuDevice"/> chooses the most efficient 
/// supported execution strategy based on its specific hardware capabilities
/// (e.g., the preference for GPU over SIMD and SIMD over scalar).
/// </remarks>
public enum ComputeMode : byte
{
    /// <summary> Automatically selects the optimal mode based on device capabilities. </summary>
    Device  = 0,
    
    /// <summary>
    /// Executes operations using scalar CPU instructions. <br/>
    /// This mode enables simple debugging of your <c>[Kernel]</c> blueprint methods.
    /// </summary>
    /// 
    Scalar  = 1,
    
    /// <summary> Executes operations using SIMD CPU instructions. </summary>
    SIMD    = 2,
    
    /// <summary> Executes operations using GPU compute shaders. </summary>
    GPU     = 3
}

