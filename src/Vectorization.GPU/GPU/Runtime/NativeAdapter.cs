// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU.Runtime;

public abstract class NativeAdapter : IDisposable
{
    public  abstract    bool        IsDisposed { get; }
    
    public abstract void            Dispose();
    
    public abstract GpuDevice       CreateDevice(string label, int maxTasks, int slotSize);
    public abstract GpuAdapterInfo  GetAdapterProperties ();
}