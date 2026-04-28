using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Silk.NET.WebGPU;


// ReSharper disable InconsistentNaming
namespace Tests.Generators.Lab;

public class GpuBuffer<T> {
    public readonly IntPtr      Handle;
    public readonly GpuContext  Context;  // Creator of GpuBuffer
    public          int         Length => throw new NotImplementedException(); 
    public          GpuTask     LastWritingTask;

//  public readonly unsafe Buffer* Ptr;

    public GpuBuffer(GpuContext ctx, uint size) 
    {
        Context = ctx;
        // Ptr = ctx.CreateBuffer(size); ...
    }
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

public class BindGroupLayoutBuilder
{
    private readonly List<BindGroupLayoutEntry> _entries;
    
    public BindGroupLayoutBuilder AddBuffer<T>(int binding) where T : struct
    {
        _entries.Add(new BindGroupLayoutEntry {
            Binding = (uint)binding,
            Visibility = ShaderStage.Compute,       // <--- we do compute
            Buffer = new BufferBindingLayout {
                Type = BufferBindingType.Storage    // for Buffer<>'s passed to the shadow method
            }
        });
        return this;
    }

    public BindGroupLayoutBuilder AddUniform<T>(int binding) where T : struct
    {
        _entries.Add(new BindGroupLayoutEntry {
            Binding = (uint)binding,
            Visibility = ShaderStage.Compute,       // <--- we do compute
            Buffer = new BufferBindingLayout {
                Type = BufferBindingType.Uniform    // For GpuContext._uniformPool storing uniforms
            }
        });
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

public unsafe class GpuBindGroupLayout
{
    internal GpuContext context;
    internal BindGroupLayout* Handle;
    
    private static int _bindGroupLayoutSlotCount;
    
    public static int NewBindGroupLayoutSlot() => _bindGroupLayoutSlotCount++; 
}

public class GpuBindGroup
{
    public GpuBindGroup(IntPtr handle)
    {
        throw new NotImplementedException();
    }
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

public class GpuCommandBuffer
{
    internal IntPtr Handle;
}

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