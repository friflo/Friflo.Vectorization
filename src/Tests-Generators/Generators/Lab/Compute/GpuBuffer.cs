using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;


// ReSharper disable InconsistentNaming
namespace Tests.Generators.Lab;

public unsafe class GpuBuffer<T> : IDisposable where T : unmanaged
{
    public          Buffer*     Handle { get; private set; }
    public readonly GpuContext  Context;  // Creator of GpuBuffer
    public          int         Length;
    private         uint        SizeInBytes;
    public          GpuTask     LastWritingTask;

//  public readonly unsafe Buffer* Ptr;

    public GpuBuffer(GpuContext ctx, uint sizeInBytes, BufferUsage usage) 
    {
        Context = ctx;
        // Wir speichern die Größe in Bytes, falls wir später Alignment-Checks brauchen
        SizeInBytes = sizeInBytes; 
        
        // Wir berechnen die Länge basierend auf dem Typ T (z.B. float = 4 Bytes)
        Length = (int)(sizeInBytes / sizeof(T));

        // Den Pointer von der API holen
        Handle = ctx.CreateBuffer(sizeInBytes, usage);
    }
    
    public unsafe GpuBuffer(GpuContext ctx, T[] data, BufferUsage usage) 
    {
        Context = ctx;
        Length  = data.Length;
        Handle  = ctx.CreateBufferWithData(data, usage);
    }
    
    public void Dispose()
    {
        if (Handle != null) {
            Context._wgpu.BufferRelease(Handle);
            Handle = null;
        }
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
    
    public void Download(GpuBuffer<T> gpuBuffer, T[] targetArray)
    {
        if (targetArray.Length < gpuBuffer.Length)
            throw new Exception("Target array is too small!");

        uint size = (uint)(gpuBuffer.Length * sizeof(T));
        
        // 1. Staging Buffer erstellen (wie zuvor)
        var readDesc = new BufferDescriptor
        {
            Size = size,
            Usage = BufferUsage.CopyDst | BufferUsage.MapRead,
            MappedAtCreation = false
        };
        var ctx         = gpuBuffer.Context;
        var _wgpu       = ctx._wgpu;
        var DevicePtr   = ctx.DevicePtr;
        var QueuePtr    = ctx.QueuePtr;
        var readBuffer  = _wgpu.DeviceCreateBuffer(DevicePtr, &readDesc);

        // 2. GPU-interne Kopie
        var encoder = _wgpu.DeviceCreateCommandEncoder(DevicePtr, null);
        _wgpu.CommandEncoderCopyBufferToBuffer(encoder, gpuBuffer.Handle, 0, readBuffer, 0, size);
        
        var commandBuffer = _wgpu.CommandEncoderFinish(encoder, null);
        _wgpu.QueueSubmit(QueuePtr, 1, &commandBuffer);

        // 3. Asynchrones Mapping
        bool mapFinished = false;
        var callback = PfnBufferMapCallback.From((status, data) => { mapFinished = true; });
        _wgpu.BufferMapAsync(readBuffer, MapMode.Read, 0, size, callback, null);

        while (!mapFinished) {
            ctx.Poll(true);
        }

        // 4. In das ORIGINAL-Array zurückkopieren
        void* pMapped = _wgpu.BufferGetMappedRange(readBuffer, 0, size);
        fixed (void* pTarget = targetArray) {
            System.Buffer.MemoryCopy(pMapped, pTarget, size, size);
        }
        // 5. Cleanup
        _wgpu.BufferUnmap(readBuffer);
        _wgpu.BufferDestroy(readBuffer);
    }
}


internal unsafe class GpuQueue
{
    private GpuContext  Context;
    private Queue*      Handle;
    
    public GpuQueue(GpuContext ctx) {
        Context = ctx;
    }
    
    public void WriteBuffer(Buffer* buffer, uint offsetInBytes, void* data, uint byteSize)
    {
        var ctx = Context;
        ctx._wgpu.QueueWriteBuffer(ctx.QueuePtr, buffer, offsetInBytes, data, byteSize);
    }
    
    public void Submit(GpuCommandBuffer commandBuffer)
    {
        var handle = commandBuffer.Handle;
        var ctx = Context;
        ctx._wgpu.QueueSubmit(ctx.QueuePtr, 1, &handle);
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
    private readonly GpuContext Context;
    
    private readonly List<BindGroupLayoutEntry> _entries = new();
    
    internal BindGroupLayoutBuilder(GpuContext ctx) {
        Context = ctx;
    }
    
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

public unsafe class GpuComputePass : IDisposable {
    private readonly    GpuEncoder          _encoder;
    public              ComputePassEncoder* Handle { get; }
    private             bool                _hasEnded = false;
    
    public GpuComputePass(GpuEncoder encoder, ComputePassEncoder* handle)
    {
        _encoder = encoder;
        Handle   = handle;
    }
    
    public void Dispose() {
        End(); // Sicherstellen, dass der Pass beendet wurde
        // Den nativen Pass-Encoder freigeben
        if (Handle != null) _encoder.Context._wgpu.ComputePassEncoderRelease(Handle);
    }

    public void SetPipeline(GpuComputePipeline pipeline)
    {
        _encoder.Context._wgpu.ComputePassEncoderSetPipeline(Handle, pipeline.Handle);
    }
    
    public void DispatchWorkgroups(int workgroupCountX, int workgroupCountY, int workgroupCountZ) {
        _encoder.Context._wgpu.ComputePassEncoderDispatchWorkgroups(
            Handle, 
            (uint)workgroupCountX, 
            (uint)workgroupCountY, 
            (uint)workgroupCountZ
        );
    }

    public void End()
    {
        if (!_hasEnded) {
            _encoder.Context._wgpu.ComputePassEncoderEnd(Handle);
            _hasEnded = true;
        }
    }

    public void SetBindGroup(int groupIndex, GpuBindGroup bindGroup)
    {
        // Der vierte und fünfte Parameter sind für dynamische Offsets (hier 0/null)
        _encoder.Context._wgpu.ComputePassEncoderSetBindGroup(Handle, (uint)groupIndex, bindGroup.Handle, 0, null);
    }
}

public unsafe class GpuBindGroupLayout
{
    internal GpuContext         context;
    internal BindGroupLayout*   Handle;
}

public unsafe class GpuBindGroup
{
    public BindGroup* Handle { get; }
    
    public GpuBindGroup(BindGroup* handle) {
        Handle = handle;
    }
}

public unsafe class GpuEncoder : IDisposable
{
    public readonly GpuContext      Context;
    public          CommandEncoder* Handle { get; }
    
    public GpuEncoder(GpuContext ctx)
    {
        Context = ctx;
        // Erstellt den nativen Encoder
        CommandEncoderDescriptor desc = new CommandEncoderDescriptor { Label = null };
        Handle = Context._wgpu.DeviceCreateCommandEncoder(Context.DevicePtr, &desc);
    }
    
    public void Dispose() {
        if (Handle != null) Context._wgpu.CommandEncoderRelease(Handle);
    }

    public GpuCommandBuffer Finish() {
        // Finish() macht aus dem Encoder einen fertigen CommandBuffer
        CommandBufferDescriptor desc = new CommandBufferDescriptor { Label = null };
        var commandBufferHandle = Context._wgpu.CommandEncoderFinish(Handle, &desc);
        return new GpuCommandBuffer(commandBufferHandle);
    }
    
    // --- ComputePass methods
    public GpuComputePass BeginComputePass()
    {
        ComputePassDescriptor desc = new ComputePassDescriptor { Label = null };
        var passHandle = Context._wgpu.CommandEncoderBeginComputePass(Handle, &desc);
        
        return new GpuComputePass(this, passHandle);
    }
}

public unsafe class GpuCommandBuffer : IDisposable
{
    internal CommandBuffer* Handle;
    
    internal GpuCommandBuffer(CommandBuffer* handle) {
        Handle = handle;
    }
    
    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
