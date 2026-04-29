// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Silk.NET.WebGPU;

namespace Friflo.Vectorization.GPU;

public unsafe class GpuCommandBuffer : IDisposable
{
    internal    GpuContext      Context;
    internal    CommandBuffer*  Handle;
    
    internal GpuCommandBuffer(GpuContext context) {
        Context = context;
    }
    
    ~GpuCommandBuffer() {
        Dispose(); // if User forgets to call
    }
    
    public void Dispose()
    {
        if (Handle == null) return;

        // WebGPU native Release
        Context._wgpu.CommandBufferRelease(Handle);

        // Die Lebensversicherung: Handle auf null setzen
        Context = null;

        // GC mitteilen, dass das Objekt abgehakt ist
        GC.SuppressFinalize(this);
    }
}
