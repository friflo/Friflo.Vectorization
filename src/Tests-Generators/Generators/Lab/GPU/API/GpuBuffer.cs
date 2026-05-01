// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization.GPU.Runtime;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;


// ReSharper disable InconsistentNaming
namespace Friflo.Vectorization.GPU;

public sealed unsafe class GpuBuffer<T> : IDisposable where T : unmanaged
{
    internal            Buffer*     handle { get; private set; }
    internal readonly   GpuDevice   device;
    public              int         Length;
    private             uint        SizeInBytes;
    public              GpuTask     LastWritingTask;

    public GpuBuffer(GpuDevice device, uint sizeInBytes, BufferUsage usage) 
    {
        this.device = device;
        // Wir speichern die Größe in Bytes, falls wir später Alignment-Checks brauchen
        SizeInBytes = sizeInBytes; 
        
        // Wir berechnen die Länge basierend auf dem Typ T (z.B. float = 4 Bytes)
        Length = (int)(sizeInBytes / sizeof(T));

        // Den Pointer von der API holen
        handle = device.CreateBuffer(sizeInBytes, usage);
    }
    
    public GpuBuffer(GpuDevice device, T[] data, BufferUsage usage) 
    {
        this.device = device;
        Length  	= data.Length;
        handle  	= device.CreateBufferWithData(data, usage);
    }
    
    public void Dispose()
    {
        if (handle != null) {
            device.wgpu.BufferRelease(handle);
            handle = null;
        }
    }
    
    public T this[int index]
    {
        get {
            if (LastWritingTask != null && !LastWritingTask.IsCompleted) {
                device.Wait(this); // force Compute before CPU reads value
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
        if (!device.DebugMode) {
            return;
        }
        device.Flush();
    }
    
    public void Download(GpuBuffer<T> gpuBuffer, T[] targetArray) // TODO  optimize DeviceCreateBuffer und DeviceCreateCommandEncoder are heavy operations
    {
        var dev = device;
        dev.Flush();
        
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
        var wgpu        = dev.wgpu;
        var DevicePtr   = dev.DevicePtr;
        var QueuePtr    = dev.QueuePtr;
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
            dev.Poll(true);
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


