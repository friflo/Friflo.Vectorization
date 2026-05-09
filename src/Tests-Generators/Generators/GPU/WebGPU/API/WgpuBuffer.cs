// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU._Native;
using Friflo.Vectorization.GPU.Runtime;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;


// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;



public sealed unsafe class WgpuBuffer<T> : NativeBuffer<T> where T : unmanaged
{
    private readonly    string      label;
    internal            Buffer*     handle { get; private set; }
    private             WgpuDevice  Device { get; set; }
    private readonly    WebGPU      wgpu;
    public  readonly    int         Length;
    private	readonly    long        Id;
    private readonly    uint        SizeInBytes;
    public  override    bool        IsDisposed => handle == null;
    
    public  override    string      ToString() => $"{label}({Id}): {(handle == null ? "Disposed" : "Alive")}";


    // Every class implementing IDispose must follow the same pattern. Set GpuInstance code sample.
    public override void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
   ~WgpuBuffer() {
        Dispose(false);  // false: release only native pointers.
    }
    
    private void Dispose(bool disposing)
    {
        if (handle == null) return;
        wgpu.BufferRelease(handle);
        handle = null;
        Device = null;
    }

    internal WgpuBuffer(WgpuDevice device, Buffer* buffer, int length, string bufferLabel, long id)
    {
        label       = bufferLabel;
        Device      = device;
        wgpu        = device.wgpu;
        SizeInBytes = (uint)(length * Unsafe.SizeOf<T>());
        Length      = length;
        Id          = id;
        handle      = buffer;
    }
    
    public T this[int index]
    {
        get {
            if (LastWritingTask != null && !LastWritingTask.IsCompleted) {
                Device.Wait<T>(this); // force Compute before CPU reads value
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
        if (!Device.DebugMode) {
            return;
        }
        Device.Flush();
    }
    
    public override void Download(GpuBuffer<T> gpuBuffer, T[] targetArray) // TODO  optimize DeviceCreateBuffer und DeviceCreateCommandEncoder are heavy operations
    {
        var dev = Device;
        dev.Flush();
        
        if (targetArray.Length < gpuBuffer.Length)
            throw new Exception("Target array is too small!");

        uint size = (uint)(gpuBuffer.Length * sizeof(T));
        
        var readDesc = new BufferDescriptor {
            Size = size,
            Usage = BufferUsage.CopyDst | BufferUsage.MapRead,
            MappedAtCreation = false
        };
        var wgpu        = dev.wgpu;
        var DevicePtr   = dev.DevicePtr;
        var QueuePtr    = dev.QueuePtr;
        var readBuffer  = wgpu.DeviceCreateBuffer(DevicePtr, &readDesc);

        var encoder = wgpu.DeviceCreateCommandEncoder(DevicePtr, null);
        wgpu.CommandEncoderCopyBufferToBuffer(encoder, ((WgpuBuffer<T>)gpuBuffer._native).handle, 0, readBuffer, 0, size);
        
        var commandBuffer = wgpu.CommandEncoderFinish(encoder, null);
        wgpu.QueueSubmit(QueuePtr, 1, &commandBuffer);  // releases commandBuffer
        wgpu.CommandEncoderRelease(encoder);            // Not sure if required.
        wgpu.CommandBufferRelease(commandBuffer);       // Not sure if required. QueueSubmit() seems to release

        // asynchronous mapping
        bool mapFinished = false;
        var callback = PfnBufferMapCallback.From((_, _) => { mapFinished = true; });
        wgpu.BufferMapAsync(readBuffer, MapMode.Read, 0, size, callback, null);

        while (!mapFinished) {
            dev.Poll(true);
        }

        // get result back in original array
        void* pMapped = wgpu.BufferGetMappedRange(readBuffer, 0, size);
        fixed (void* pTarget = targetArray) {
            System.Buffer.MemoryCopy(pMapped, pTarget, size, size);
        }
        // cleanup
        wgpu.BufferUnmap(readBuffer);
        wgpu.BufferDestroy(readBuffer);
        wgpu.BufferRelease(readBuffer);
    }
}


