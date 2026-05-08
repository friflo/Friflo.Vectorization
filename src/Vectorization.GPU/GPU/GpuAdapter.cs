// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization.GPU.Runtime;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

public sealed class GpuAdapter : IDisposable
{
    private readonly    NativeAdapter   native;
    public              bool            IsDisposed => native.IsDisposed;
    
    public  override    string          ToString() => native.ToString();
    
    internal GpuAdapter(NativeAdapter native) {
        this.native = native;
    }
    
    public void             Dispose()               => native.Dispose();
    
    public GpuDevice        CreateDevice(string label, int maxTasks = 64, int slotSize = 64 * 1024) => native.CreateDevice(label, maxTasks, slotSize);
    public GpuAdapterInfo   GetAdapterProperties () => native.GetAdapterProperties();
}

