using System;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

namespace Tests.Generators.Lab;

public class GpuBuffer<T> {
    public readonly GpuContext Context;  // Creator of GpuBuffer
//  public readonly unsafe Buffer* Ptr;

    public GpuBuffer(GpuContext ctx, uint size) 
    {
        Context = ctx;
        // Ptr = ctx.CreateBuffer(size); ...
    }
}

public unsafe class GpuContext : IDisposable
{
    public Wgpu*    WgpuPtr { get; }
    public Device*  DevicePtr { get; }
    public Queue*   QueuePtr { get; }

    public GpuContext() { }

    public void Dispatch(Buffer<byte> w, Buffer<float> i, float u) 
    {
        // feed CommandEncoder
    }

    public void Dispose() { /* Cleanup native resources */ }
}