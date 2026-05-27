// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

public class PipelineContext
{
    public virtual bool EnablePassBatching { get; set; } 
} 

public abstract class GpuDevice : IDisposable
{
    public  readonly    string  Label;
    public  readonly    int     SlotSize;
    public              bool    DebugMode   { get; set; } 
    
    public  override    string  ToString() => Label + (IsDisposed ? ": Disposed" : ": Alive");

    protected GpuDevice(string label, int slotSize) {
        Label       = label;
        SlotSize    = slotSize;
    }
    
    public IReadOnlyGpuBuffer<T> CreateReadOnlyBuffer<T>    (T[] data, string label, BufferType type = BufferType.Storage) where T : unmanaged {
        return CreateBuffer(data, label, BufferProfile.StaticIn, type);
    }
    
    public IScopedReadBuffer<T>  CreateScopedReadBuffer<T>  (T[] data, string label, BufferType type = BufferType.Storage) where T : unmanaged {
        return CreateBuffer(data, label, BufferProfile.InOut, type);
    }
    
    public IScopedWriteBuffer<T> CreateScopedWriteBuffer<T> (T[] data, string label, BufferType type = BufferType.Storage) where T : unmanaged {
        return CreateBuffer(data, label, BufferProfile.InOut, type);
    }
    
    public IScopedGpuBuffer<T>   CreateScopedBuffer<T>      (T[] data, string label, BufferType type = BufferType.Storage) where T : unmanaged {
        return CreateBuffer(data, label, BufferProfile.InOut, type);
    }

    // --- abstract
    public abstract ComputeMode     DefaultComputeMode  { get; }
    public abstract PipelineContext PipelineContext     { get; }

    public abstract bool            IsDisposed          { get; }
    public abstract void            Dispose();
    
    public abstract GpuLimits       GetDeviceLimits();
    public abstract GpuBuffer<T>    CreateBuffer<T>(int length, string label, BufferProfile profile, BufferType type = BufferType.Storage) where T : unmanaged;
    public abstract GpuBuffer<T>    CreateBuffer<T>(T[] data,   string label, BufferProfile profile, BufferType type = BufferType.Storage) where T : unmanaged;

    public abstract void            Flush(bool wait = true);    // TODO - remove parameter wait 
//  public abstract void            Synchronize();              // planned
    public abstract void            Download();
}

/// <summary>  Defines the execution strategy for compute operations. </summary>
/// <remarks>
/// The <see cref="ComputeMode"/> determines which backend (CPU or GPU)  is used to perform calculations.<br/>
/// When <see cref="Device"/> is selected, the <see cref="GpuDevice"/> chooses the most efficient 
/// supported execution strategy based on its specific hardware capabilities
/// (e.g., preferring GPU over SIMD, and SIMD over Scalar).
/// </remarks>
public enum ComputeMode
{
    /// <summary> Automatically selects the optimal mode based on device capabilities. </summary>
    Device  = 0,
    
    /// <summary>
    /// Executes operations using scalar CPU instructions. <br/>
    /// This mode enables simple debugging of your <c>[Kernel]</c> blueprint method.
    /// </summary>
    /// 
    Scalar  = 1,
    
    /// <summary> Executes operations using SIMD CPU instructions. </summary>
    SIMD    = 2,
    
    /// <summary> Executes operations using GPU compute shaders. </summary>
    GPU     = 3
}

