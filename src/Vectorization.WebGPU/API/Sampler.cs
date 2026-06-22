// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


public sealed unsafe partial class WgpuDevice
{
    public GpuSampler CreateSampler(SamplerDescriptor desc)
    {
        // desc.addressModeU = AddressMode.Repeat;  // TODO necessary?
        // desc.addressModeV = AddressMode.Repeat;
        if (desc.maxAnisotropy == 0) {
            desc.maxAnisotropy = 1;
        }
        Sampler* sampler = wgpuDeviceCreateSampler(DevicePtr, &desc);
        return new GpuSampler(sampler);
    }
}



public sealed unsafe class GpuSampler : IDisposable
{
    private Sampler* handle;
    
    internal GpuSampler(Sampler* handle) {
        this.handle = handle;
    }
    
    public void Dispose()
    {
        if (handle != null) {
            wgpuSamplerRelease(handle);
            handle = null;
        }
    }
}
