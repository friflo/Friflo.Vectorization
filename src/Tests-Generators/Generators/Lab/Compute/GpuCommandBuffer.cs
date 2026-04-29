using System;
using Silk.NET.WebGPU;

namespace Tests.Generators.Lab;

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
