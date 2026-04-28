using System;
using System.Runtime.CompilerServices;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

// ReSharper disable InconsistentNaming
namespace Tests.Generators.Lab;

public class GpuBuffer<T> {
    public readonly IntPtr      Handle;
    public readonly GpuContext  Context;  // Creator of GpuBuffer
    public          int         Length => throw new NotImplementedException(); 
//  public readonly unsafe Buffer* Ptr;

    public GpuBuffer(GpuContext ctx, uint size) 
    {
        Context = ctx;
        // Ptr = ctx.CreateBuffer(size); ...
    }
}

public unsafe class GpuContext : IDisposable
{
    public Wgpu*    WgpuPtr     { get; }
    public Device*  DevicePtr   { get; }
    public Queue*   QueuePtr    { get; }
    
    private GpuBindGroupLayout[] bindGroupSlots;
    
    public GpuBindGroupLayout GetBindGroupLayout(int slot) {
        return bindGroupSlots[slot];
    }
    
    public void SetBindGroupLayout(int slot, GpuBindGroupLayout layout) {
        bindGroupSlots[slot] = layout;
    }

    public GpuContext()
    {
        // _uniformPool = CreateBuffer<byte>(64 * 1024, BufferUsage.Uniform | BufferUsage.CopyDst);
    }

    public void Dispatch(Buffer<byte> w, Buffer<float> i, float u) 
    {
        // feed CommandEncoder
    }

    public void Dispose() { /* Cleanup native resources */ }

    public GpuBatch BeginBatch() {
        return new GpuBatch();
    }

    public GpuEncoder CreateEncoder() {
        throw new NotImplementedException();
    }

    public void Submit(GpuCommandBuffer commandBuffer) {
        throw new NotImplementedException();
    }
    
    public GpuPipeline GetPipeline(string shaderName) {
        return new GpuPipeline();
    }

    public BinGroupLayoutBuilder BindGroupLayoutBuilder()
    {
        throw new NotImplementedException();
    }

    public GpuBindGroup CreateBindGroup(GpuBindGroupLayout layout, Span<GpuBindEntry> bindEntries)
    {
        throw new NotImplementedException();
    }

    private GpuBuffer<byte> _uniformPool;
    private uint            _poolOffset = 0;
    
    public GpuBindEntry AsUniformEntry<T>(int binding, T value) where T : struct
    {
        uint size           = (uint)sizeof(T);
        uint alignedOffset  = (_poolOffset + 255) & ~255u;                      // WebGPU requires Uniform offset must by 256 byte aligned
        WriteBuffer(_uniformPool, alignedOffset, &value, size);                 // write value in _uniformPool
        _poolOffset = alignedOffset + size;
        return new GpuBindEntry(binding, _uniformPool, alignedOffset, size);    // use _uniformPool at alignedOffset
    }
    
    private GpuQueue _queue;
    
    private void WriteBuffer<T>(GpuBuffer<T> buffer, uint byteOffset, void* data, uint byteSize) where T : unmanaged
    {
        _queue.WriteBuffer(
            buffer.Handle,
            byteOffset,        // offset in buffer
            data,              // pointer on my value
            byteSize           // value size
        );
    }

    public void ResetPool() => _poolOffset = 0; // Am Ende des Frames/Batches rufen
}

public class GpuPipeline
{
    
}

internal class GpuQueue
{
    public unsafe void WriteBuffer(IntPtr bufferHandle, uint byteOffset, void* data, uint byteSize)
    {
        // wgpuQueueWriteBuffer(_handle, buffer, offset, data, size);
    }
    
    public void Submit(GpuCommandBuffer commandBuffer)
    {
        // wgpuQueueSubmit(_handle, 1, &commandBuffer);
    }
}

public struct GpuBindEntry
{
    public uint     Binding;
    public IntPtr   BufferHandle; // or Type: IGpuBuffer interface that shares oll GpuBuffer<T>'s
    public uint     Offset;
    public uint     Size;
    
    public static GpuBindEntry From<T>(int binding, GpuBuffer<T> buffer) where T : struct {
        return new GpuBindEntry(binding, buffer.Handle, 0, (uint)(Unsafe.SizeOf<T>() * buffer.Length));
    }

    public GpuBindEntry(int binding, GpuBuffer<byte> pool, uint offset, uint size) 
        : this(binding, pool.Handle, offset, size) { }

    private GpuBindEntry(int binding, IntPtr handle, uint offset, uint size) {
        Binding         = (uint)binding;
        BufferHandle    = handle;
        Offset          = offset;
        Size            = size;
    }
}

public class BinGroupLayoutBuilder
{
    public BinGroupLayoutBuilder AddBuffer<T>(int binding) where T : struct
    {
        return this;
    }

    public BinGroupLayoutBuilder AddUniform<T>(int binding) where T : struct
    {
        return this;
    }

    public GpuBindGroupLayout Build()
    {
        throw new NotImplementedException();
    }
}

public class GpuComputePass : IDisposable {
    public void Dispose() {
        throw new NotImplementedException();
    }

    public void SetPipeline(GpuPipeline computePass)
    {
        throw new NotImplementedException();
    }
    
    public void DispatchWorkgroups(int workgroupCountX, int workgroupCountY, int workgroupCountZ) {
        
    }

    public void End()
    {
        throw new NotImplementedException();
    }

    public void SetBindGroup(int groupIndex, GpuBindGroup bindGroup)
    {
        throw new NotImplementedException();
    }
}

public class GpuBindGroupLayout
{
    private static int _bindGroupLayoutSlotCount;
    
    public static int NewBindGroupLayoutSlot() => _bindGroupLayoutSlotCount++; 
}

public class GpuBindGroup
{
}

public class GpuEncoder : IDisposable
{
    public readonly GpuContext context;
    
    public void Dispose() {
    }
    
    public GpuCommandBuffer Finish() {
        return new GpuCommandBuffer();
    }

    public GpuTask Submit() {
        return new GpuTask(context);
    }
    // --- ComputePass methods
    public GpuComputePass BeginComputePass()
    {
        throw new NotImplementedException();
    }
}

public class GpuCommandBuffer { }

public class GpuBatch : IDisposable
{
    private readonly GpuContext context;
    
    public GpuEncoder Encoder { get; }
    
    public void Dispose() {
    }

    public GpuTask Submit() {
        return new GpuTask(context);
    }
}