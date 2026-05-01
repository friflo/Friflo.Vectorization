// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;


// ReSharper disable InconsistentNaming
namespace Friflo.Vectorization.GPU;

public sealed unsafe class GpuBuffer<T> : IDisposable where T : unmanaged
{
    internal            Buffer*     handle { get; private set; }
    internal readonly   GpuContext  context;
    public              int         Length;
    private             uint        SizeInBytes;
    public              GpuTask     LastWritingTask;

    public GpuBuffer(GpuContext ctx, uint sizeInBytes, BufferUsage usage) 
    {
        context = ctx;
        // Wir speichern die Größe in Bytes, falls wir später Alignment-Checks brauchen
        SizeInBytes = sizeInBytes; 
        
        // Wir berechnen die Länge basierend auf dem Typ T (z.B. float = 4 Bytes)
        Length = (int)(sizeInBytes / sizeof(T));

        // Den Pointer von der API holen
        handle = ctx.CreateBuffer(sizeInBytes, usage);
    }
    
    public GpuBuffer(GpuContext ctx, T[] data, BufferUsage usage) 
    {
        context = ctx;
        Length  = data.Length;
        handle  = ctx.CreateBufferWithData(data, usage);
    }
    
    public void Dispose()
    {
        if (handle != null) {
            context.wgpu.BufferRelease(handle);
            handle = null;
        }
    }
    
    public T this[int index]
    {
        get {
            if (LastWritingTask != null && !LastWritingTask.IsCompleted) {
                context.Wait(this); // force Compute before CPU reads value
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
        if (!context.DebugMode) {
            return;
        }
        context.Flush();
    }
    
    public void Download(GpuBuffer<T> gpuBuffer, T[] targetArray) // TODO  optimize DeviceCreateBuffer und DeviceCreateCommandEncoder are heavy operations
    {
        context.Flush();
        
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
        var ctx         = gpuBuffer.context;
        var wgpu        = ctx.wgpu;
        var DevicePtr   = ctx.DevicePtr;
        var QueuePtr    = ctx.QueuePtr;
        var readBuffer  = wgpu.DeviceCreateBuffer(DevicePtr, &readDesc);

        // 2. GPU-interne Kopie
        var encoder = wgpu.DeviceCreateCommandEncoder(DevicePtr, null);
        wgpu.CommandEncoderCopyBufferToBuffer(encoder, gpuBuffer.handle, 0, readBuffer, 0, size);
        
        var commandBuffer = wgpu.CommandEncoderFinish(encoder, null);
        wgpu.QueueSubmit(QueuePtr, 1, &commandBuffer);

        // 3. Asynchrones Mapping
        bool mapFinished = false;
        var callback = PfnBufferMapCallback.From((_, _) => { mapFinished = true; });
        wgpu.BufferMapAsync(readBuffer, MapMode.Read, 0, size, callback, null);

        while (!mapFinished) {
            ctx.Poll(true);
        }

        // 4. In das ORIGINAL-Array zurückkopieren
        void* pMapped = wgpu.BufferGetMappedRange(readBuffer, 0, size);
        fixed (void* pTarget = targetArray) {
            System.Buffer.MemoryCopy(pMapped, pTarget, size, size);
        }
        // 5. Cleanup
        wgpu.BufferUnmap(readBuffer);
        wgpu.BufferDestroy(readBuffer);
    }
}


