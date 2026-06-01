// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;
using Buffer = Friflo.Vectorization.WebGPU.Runtime.Buffer;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable InlineTemporaryVariable
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU;


internal unsafe interface IWgpuBuffer {
    internal    BufferData  GetBufferData();
    internal    void        ExecuteCpuCopy(void* pMapped, List<BufferRange> bufferRanges);
}

public sealed unsafe class WgpuBuffer<T> : GpuBuffer<T>, IWgpuBuffer where T : unmanaged
{
    internal            Buffer*     handle { get; private set; }
    private             WgpuDevice  device { get; set; }
    private  readonly   uint        SizeInBytes;    
    private             BufferData  data;
    // --- GpuBuffer
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
        wgpuBufferRelease(data.stagingHandle);
        handle = null;
        device = null;
        data.stagingHandle = null;
    }

    internal WgpuBuffer(WgpuDevice device, Buffer* buffer, int bufferId, Buffer* statingHandle, Memory<T> hostMemory, string bufferLabel)
        : base(hostMemory, bufferLabel, (nint)buffer, bufferId)
    {
        this.device     = device;
        SizeInBytes     = (uint)(Length * Unsafe.SizeOf<T>());
        handle          = buffer;
        data            = new BufferData(bufferId, Marshal.SizeOf<T>(), Length, buffer, statingHandle);
    }
    
    // TODO obsolete - kept temporary for reference - will be removed
#if DEBUG
    private void Download(GpuBuffer<T> gpuBuffer, T[] targetArray) // TODO  optimize DeviceCreateBuffer und DeviceCreateCommandEncoder are heavy operations
    {
        var dev = device;
        // dev.Flush();  irrelevant code
        
        if (targetArray.Length < gpuBuffer.Length)
            throw new Exception("Target array is too small!");

        uint size = (uint)(gpuBuffer.Length * sizeof(T));
        
        var readDesc = new BufferDescriptor {
            size = size,
            usage = (ulong)(BufferUsage.CopyDst | BufferUsage.MapRead),
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
        // var callback = PfnBufferMapCallback.From((_, _) => { mapFinished = true; });
        var callbackInfo = new BufferMapCallbackInfo {
            mode        = CallbackMode.WaitAnyOnly, // requires blocking wgpuInstanceWaitAny()
            callback    = &BufferUtils.BufferMap_callback,
            userdata1   = &mapFinished
        };
        var future = wgpuBufferMapAsync(readBuffer, (ulong)MapMode.Read, 0, size, callbackInfo);
        if (future.id != 0) {
            var waitInfo = new FutureWaitInfo { future = future, completed = 0 };
            wgpuInstanceWaitAny(device.instance, 1, &waitInfo, uint.MaxValue);
        }
        while (!mapFinished) {   // used in wgpu v19
            // same as: wgpuDevicePoll(DevicePtr, WgpuUtils.FromBool(true), null);
            wgpuInstanceProcessEvents(device.instance);
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
#endif
    
    // --- IWgpuBuffer
    BufferData IWgpuBuffer.GetBufferData() => data;
    
    void IWgpuBuffer.ExecuteCpuCopy(void* pMapped, List<BufferRange> requestedRanges)
    {
        ReadOnlySpan<T>     gpuSourceSpan   = new ReadOnlySpan<T>(pMapped, Length);
        Span<T>             hostTargetSpan  = hostMemory.Span;
        Span<BufferRange>   ranges          = CollectionsMarshal.AsSpan(requestedRanges);

        // iterate unoptimized requested ranges
        foreach (var range in ranges)
        {
            int start   = range.start;
            int length  = range.length;

            ReadOnlySpan<T>  sourceSlice = gpuSourceSpan.Slice (start, length);
            Span<T>          targetSlice = hostTargetSpan.Slice(start, length);

            sourceSlice.CopyTo(targetSlice);
        }
    }
}

#if DEBUG
// TODO obsolete - was used by Download() - will be removed
internal static class BufferUtils
{
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    internal static unsafe void BufferMap_callback(MapAsyncStatus status, StringView message, void* userdata1, void* userdata2) {
        if (userdata1== null) return;
        var mapFinished = (bool*)userdata1;
        *mapFinished = true;
    }
}
#endif

