// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Silk.NET.WebGPU;

namespace Friflo.Vectorization.GPU;

public sealed unsafe class GpuCommandBuffer : IDisposable
{
    internal    GpuContext      context;
    internal    CommandBuffer*  handle;
    
    internal GpuCommandBuffer(GpuContext context) {
        this.context = context;
    }
    
    ~GpuCommandBuffer() {
        Dispose(); // if User forgets to call
    }
    
    public void Dispose()
    {
        if (handle == null) return;

        // WebGPU native Release
        context.wgpu.CommandBufferRelease(handle);

        // Die Lebensversicherung: Handle auf null setzen
        context = null;

        // GC mitteilen, dass das Objekt abgehakt ist
        GC.SuppressFinalize(this);
    }
}
