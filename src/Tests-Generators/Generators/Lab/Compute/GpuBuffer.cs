using System;
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
        Context.Flush();
    }
    
    public void Download(GpuBuffer<T> gpuBuffer, T[] targetArray) // TODO  optimize DeviceCreateBuffer und DeviceCreateCommandEncoder are heavy operations
    {
        Context.Flush();
        
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

