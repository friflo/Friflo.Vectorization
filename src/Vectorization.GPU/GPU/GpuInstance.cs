// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization.GPU.Runtime;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;


public sealed class GpuInstance : IDisposable
{
    private readonly    NativeInstance  native; 
    public              bool            IsDisposed => native.IsDisposed;
    
    public  override    string          ToString() => native.ToString();
    
    
    private GpuInstance(NativeInstance  native) {
        this.native = native;
    }
    
    public void Dispose() => native.Dispose();

    public static GpuInstance CreateInstance(InstanceExtras instanceExtras = default)
    {
        var native = WgpuInstance.CreateWgpuInstance(instanceExtras);       // TODO - Create via enum ComputeType
        return new GpuInstance(native);
    }
    
    public GpuAdapter       RequestAdapter(RequestAdapterOptions options, GpuAdapterInfo adapterInfo = null) => native.RequestAdapter(options, adapterInfo);
    public GlobalReport     GenerateReport ()       => native.GenerateReport();
    public GpuAdapterInfo[] GetAdapterProperties()  => native.GetAdapterProperties();
}

