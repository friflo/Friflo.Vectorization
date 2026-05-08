// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU.Runtime;

public abstract class NativeInstance : IDisposable
{
    public  abstract bool        IsDisposed  { get; }
    
    public abstract void Dispose();

    public static GpuInstance CreateInstance(InstanceExtras instanceExtras)
    {
        throw new  NotImplementedException();
    }
    
    public abstract GpuAdapter          RequestAdapter(RequestAdapterOptions options, GpuAdapterInfo adapterInfo);
    
    public abstract GlobalReport        GenerateReport ();
    
    public abstract GpuAdapterInfo[]    GetAdapterProperties();
}
