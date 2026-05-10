// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using Friflo.Vectorization.GPU._Native;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

public sealed class GpuDevice : IDisposable
{
    private  readonly   string          Label;
    public              bool            DebugMode   { get; set; } 
    internal readonly   int             SlotSize;
    [EditorBrowsable(EditorBrowsableState.Never)]
    public   readonly   NativeDevice    _native;

    public              bool            IsDisposed => _native.IsDisposed;
    public  override    string          ToString() => _native.ToString();
    
    
    public GpuDevice(NativeDevice native, string label, int slotSize) {
        _native     = native;
        Label       = label;
        SlotSize    = slotSize;
    }
    
    public void Dispose() => _native.Dispose();
    
    public GpuBuffer<T> CreateBuffer<T>(int length, GpuBufferUsage usage, string bufferLabel) where T : unmanaged {
        var id      = GpuBufferUtils.NextId();
        var buffer  = _native.CreateBuffer<T>(length, usage, bufferLabel, id);
        return new GpuBuffer<T>(this, buffer, length, bufferLabel, id);
    }

    public GpuBuffer<T> CreateBuffer<T>(T[] data, GpuBufferUsage usage, string bufferLabel) where T : unmanaged {
        var id      = GpuBufferUtils.NextId();
        var buffer  = _native.CreateBuffer(data, usage, bufferLabel, id);
        return new GpuBuffer<T>(this, buffer, data.Length, bufferLabel, id);
    }

    // -------------------------------- Task Dependency Tracking --------------------------------
    public void Flush(bool wait = true)                             => _native.Flush(wait);
    public void Wait<T>(GpuBuffer<T> buffer) where T : unmanaged    => _native.Wait(buffer._native);
    public void SubmitGraph(NativeTask finalTask)                   => _native.SubmitGraph(finalTask);
}

