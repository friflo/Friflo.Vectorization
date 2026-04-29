using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;


// ReSharper disable InconsistentNaming
namespace Tests.Generators.Lab;

public unsafe class GpuBuffer<T> where T : unmanaged
{
    public readonly Buffer*     Handle;
    public readonly GpuContext  Context;  // Creator of GpuBuffer
    public          int         Length; 
    public          GpuTask     LastWritingTask;

//  public readonly unsafe Buffer* Ptr;

    public GpuBuffer(GpuContext ctx, uint size, BufferUsage usage) 
    {
        Context = ctx;
        // Ptr = ctx.CreateBuffer(size); ...
    }
    
    public unsafe GpuBuffer(GpuContext ctx, T[] data, BufferUsage usage) 
    {
        Context = ctx;
        Length  = data.Length;
        Handle  = ctx.CreateBufferWithData(data, usage);
    }
    
    public T this[int index]
    {
        get {
            if (LastWritingTask != null && !LastWritingTask.IsCompleted) {
                Context.Wait(this); // force Compute before CPU reads value
            }
            return InternalDownloadValue(index);
        }
    }

    private T InternalDownloadValue(int index)
    {
        throw new NotImplementedException();
    }

    public void WaitInDebug()
    {
        if (!Context.DebugMode) {
            return;
        }
        Context.Wait<T>(this);
    }
}


internal unsafe class GpuQueue
{
    private GpuContext  Context;
    private Queue*      Handle;
    
    public GpuQueue(GpuContext ctx) {
        Context = ctx;
    }
    
    
    public unsafe void WriteBuffer(Buffer* bufferHandle, uint byteOffset, void* data, uint byteSize)
    {
        // wgpuQueueWriteBuffer(_handle, buffer, offset, data, size);
    }
    
    public void Submit(GpuCommandBuffer commandBuffer)
    {
        // wgpuQueueSubmit(_handle, 1, &commandBuffer);
    }
    
    // TODO use this static method to avoid allocation by lambda
    private static unsafe void GlobalWorkDoneCallback(QueueWorkDoneStatus status, void* userData)
    {
        // Wir casten den userData Pointer zurück auf ein GCHandle
        GCHandle handle = GCHandle.FromIntPtr((IntPtr)userData);
        if (handle.Target is GpuTask task) {
            task.IsCompleted = true;
            handle.Free(); // free handle - otherwise leak
        }
    }

    public void OnSubmittedWorkDone(int i, Action<QueueWorkDoneStatus> callback) {
        // We have to pin the callback to avoid moving callback by GC
        // For high performance we need a static method
        QueueWorkDoneCallback nativeCallback = (status, userData) => {
            callback(status);
        };
        Context._wgpu.QueueOnSubmittedWorkDone(Handle, nativeCallback, null);
        throw new NotImplementedException();
    }
}

public unsafe struct GpuBindEntry
{
    public uint     Binding;
    public Buffer*  BufferHandle; // or Type: IGpuBuffer interface that shares oll GpuBuffer<T>'s
    public uint     Offset;
    public uint     Size;
    
    public static GpuBindEntry From<T>(int binding, GpuBuffer<T> buffer) where T : unmanaged {
        return new GpuBindEntry(binding, buffer.Handle, 0, (uint)(Unsafe.SizeOf<T>() * buffer.Length));
    }

    public GpuBindEntry(int binding, GpuBuffer<byte> pool, uint offset, uint size) 
        : this(binding, pool.Handle, offset, size) { }

    private GpuBindEntry(int binding, Buffer* handle, uint offset, uint size) {
        Binding         = (uint)binding;
        BufferHandle    = handle;
        Offset          = offset;
        Size            = size;
    }
}

public class BindGroupLayoutBuilder
{
    private readonly List<BindGroupLayoutEntry> _entries;
    
    public BindGroupLayoutBuilder AddBuffer<T>(int binding, string name) where T : unmanaged
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

    public BindGroupLayoutBuilder AddUniform<T>(int binding, string name) where T : unmanaged
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

    public void SetPipeline(GpuComputePipeline pipeline)
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
    internal GpuContext         context;
    internal BindGroupLayout*    Handle;
    
    private static int _bindGroupLayoutSlotCount;
    
    public static int NewGpuEffectSlot() => _bindGroupLayoutSlotCount++; 
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
    
    // --- ComputePass methods
    public GpuComputePass BeginComputePass()
    {
        throw new NotImplementedException();
    }
}

public class GpuCommandBuffer : IDisposable
{
    internal IntPtr Handle;
    
    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
