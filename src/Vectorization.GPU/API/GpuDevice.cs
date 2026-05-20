// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

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
    
    // --- abstract
    public abstract GpuExeType      ExeType     { get; }
    public abstract bool            IsDisposed  { get; }
    public abstract void            Dispose();
    
    public abstract GpuLimits       GetDeviceLimits();
    public abstract GpuBuffer<T>    CreateBuffer<T>(int length, GpuBufferUsage usage, string bufferLabel) where T : unmanaged;
    public abstract GpuBuffer<T>    CreateBuffer<T>(T[] data,   GpuBufferUsage usage, string bufferLabel) where T : unmanaged;
    
    public abstract void            Flush(bool wait = true);
    public abstract void            Wait<T>(GpuBuffer<T> buffer) where T : unmanaged;
    public abstract void            SubmitGraph(GpuTask finalTask);
}

public enum GpuExeType
{
    Scalar,
    SIMD,
    GPU
}

