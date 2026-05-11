// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;
using Buffer = Friflo.Vectorization.WebGPU.Runtime.Buffer;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU;



public sealed unsafe class WgpuBuffer<T> : GpuBuffer<T> where T : unmanaged
{
    internal            Buffer*     handle { get; private set; }
    private             WgpuDevice  device { get; set; }
    private   readonly  uint        SizeInBytes;

    protected override  Span<T>     Span        => default;
    public    override  GpuDevice   Device      => device;
    public    override  bool        IsDisposed  => handle == null;
    
    // Every class implementing IDispose must follow the same pattern. Set GpuInstance code sample.
    public override void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
   ~WgpuBuffer() {
        Dispose(false);  // false: release only native pointers.
    }
    
    private void Dispose(bool _)
    {
        if (handle == null) return;
        wgpuBufferRelease(handle);
        handle = null;
        device = null;
    }

    internal WgpuBuffer(WgpuDevice device, Buffer* buffer, int length, string bufferLabel)
        : base(length, bufferLabel)
    {
        this.device = device;
        SizeInBytes = (uint)(length * Unsafe.SizeOf<T>());
        handle      = buffer;
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
    
    public override void Download(GpuBuffer<T> gpuBuffer, T[] targetArray) // TODO  optimize DeviceCreateBuffer und DeviceCreateCommandEncoder are heavy operations
    {
        var dev = device;
        dev.Flush();
        
        if (targetArray.Length < gpuBuffer.Length)
            throw new Exception("Target array is too small!");

        uint size = (uint)(gpuBuffer.Length * sizeof(T));
        
        var readDesc = new BufferDescriptor {
            size = size,
            usage = BufferUsage.CopyDst | BufferUsage.MapRead,
            mappedAtCreation = WgpuUtils.FromBool(false)
        };
        var DevicePtr   = dev.DevicePtr;
        var QueuePtr    = dev.QueuePtr;
        var readBuffer  = wgpuDeviceCreateBuffer(DevicePtr, &readDesc);

        var encoder = wgpuDeviceCreateCommandEncoder(DevicePtr, null);
        wgpuCommandEncoderCopyBufferToBuffer(encoder, ((WgpuBuffer<T>)gpuBuffer).handle, 0, readBuffer, 0, size);
        
        var commandBuffer = wgpuCommandEncoderFinish(encoder, null);
        wgpuQueueSubmit(QueuePtr, 1, &commandBuffer);  	// releases commandBuffer
        wgpuCommandEncoderRelease(encoder);            	// Not sure if required.
        wgpuCommandBufferRelease(commandBuffer);       	// Not sure if required. QueueSubmit() seems to release

        // asynchronous mapping
        bool mapFinished = false;
        var callback = PfnBufferMapCallback.From((_, _) => { mapFinished = true; });
        wgpuBufferMapAsync(readBuffer, MapMode.Read, 0, size, callback, null);

        while (!mapFinished) {
            dev.Poll(true);
        }

        // get result back in original array
        void* pMapped = wgpuBufferGetMappedRange(readBuffer, 0, size);
        fixed (void* pTarget = targetArray) {
            System.Buffer.MemoryCopy(pMapped, pTarget, size, size);
        }
        // cleanup
        wgpuBufferUnmap(readBuffer);
        wgpuBufferDestroy(readBuffer);
        wgpuBufferRelease(readBuffer);
    }
}


