using System;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

// ReSharper disable InconsistentNaming
namespace Tests.Generators.Lab;

public class GpuBuffer<T> {
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

    public GpuContext() { }

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
    
    public GpuComputePass GetPipeline(string shaderName) {
        return new GpuComputePass();
    }

    public BinGroupLayoutBuilder BindGroupLayoutBuilder()
    {
        throw new NotImplementedException();
    }

    public GpuBindGroup CreateBindGroup(GpuBindGroupLayout layout, Span<GpuBindEntry> bindEntries)
    {
        throw new NotImplementedException();
    }
}

public struct GpuBindEntry
{
    public GpuBindEntry(int binding, GpuBuffer<byte> buffer) { }
    public GpuBindEntry(int binding, GpuBuffer<float> inputGpuBuffer) { }
    public GpuBindEntry(int binding, float inputGpuBuffer) { }
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

    public void SetPipeline(GpuComputePass computePass)
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