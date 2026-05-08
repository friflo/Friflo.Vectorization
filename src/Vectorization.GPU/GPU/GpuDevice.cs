// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Friflo.Vectorization.GPU.Runtime;

// ReSharper disable InconsistentNaming
// ReSharper disable SwapViaDeconstruction
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

public sealed class GpuDevice : IDisposable
{
    
    internal readonly   NativeDevice    native;   
    private  readonly   string          label;
    public              bool            DebugMode   { get; set; } 
    internal readonly   int             slotSize;
    public              bool            IsDisposed => native.IsDisposed;

    public  override    string          ToString() => native.ToString();
    
    
    internal GpuDevice(NativeDevice native, string label, int slotSize) {
        this.native     = native;
        this.label      = label;
        this.slotSize   = slotSize;
    }
    
    public void Dispose() => native.Dispose();
    
    // -------------------------------- Task Dependency Tracking --------------------------------
    public void Flush(bool wait = true) => native.Flush(wait);

    public void Wait<T>(GpuBuffer<T> buffer) where T : unmanaged  => native.Wait(buffer.native);
        
    public void SubmitGraph(GpuTask finalTask) => native.SubmitGraph(finalTask);

    private IEnumerable<NativeTask> SortTasks(GpuTask finalTask) => native.SortTasks(finalTask);
}

