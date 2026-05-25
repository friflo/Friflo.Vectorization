// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Friflo.Vectorization.GPU;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable InvertIf
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed unsafe class CommandRecorder : IDisposable
{
    private  readonly   WgpuDevice          device;
    private             CommandEncoder*     currentEncoder;                 // GpuTask owns CommandEncoder* and ensures release
    internal            ComputePassEncoder* currentPass;                    // GpuTask owns ComputePassEncoder* and ensures release
    // Pre-allocated to avoid heap growth during the hot loop.
    // 4 slots cover the standard WebGPU maxBindGroups limit, ensuring a zero-allocation steady state.
    internal            BindGroups          createdBindGroups;              // GpuTask owns all created BindGroup* and ensures release  
    internal            int                 createdBindGroupsCount;
    private             CommandBuffer*      commandBuffer;
    
    private             uint                uniformOffset;             	    // cursor in pool slice used as a ring buffer
    private  readonly   byte[]              stagingBuffer;                  // CPU-cache for uniform buffer
    private  readonly   int                 slotSize;
    private  readonly   Buffer*             globalUniformPool;
    private  readonly   List<BufferRange>   requestedRanges = new();
    private             BufferEntry[]       bufferEntries   = [];

    internal readonly   List<nint>          commandBuffers  = new();
    private             int                 kernelSeq;
    private             int                 kernelId        = -1;
    private             bool                createNewPass;
    
    internal            bool                isSubmitted;        // TODO remove
    internal            bool                isCompleted;        // TODO remove

    public   override   string              ToString()      => $"newPass: {createNewPass}";

    public void Init(int id) {
        createNewPass   = kernelId != id;
        kernelId        = id;
        kernelSeq++;
    }
    
    [StackTraceHidden]
    public GpuBuffer<T> RequireRead<T>(in InBuffer<T> buffer) where T : unmanaged
    {
        var gpuBuffer   = buffer.GpuBuffer;
        var segments    = GetBufferEntry(gpuBuffer.DeviceBufferId).bufferSegments;
        createNewPass  |= SegmentKey.AddRead(segments, new SegmentKey(buffer.Offset, buffer.Length), kernelId, kernelSeq, gpuBuffer.Label);
        return gpuBuffer;
    }
    
    [StackTraceHidden]
    public GpuBuffer<T> RequireReadWrite<T>(in Buffer<T> buffer) where T : unmanaged
    {
        var gpuBuffer   = buffer.GpuBuffer;
        var segments    = GetBufferEntry(gpuBuffer.DeviceBufferId).bufferSegments;
        createNewPass  |= SegmentKey.AddReadWrite(segments, new SegmentKey(buffer.Offset, buffer.Length), kernelId, kernelSeq, gpuBuffer.Label);
        return gpuBuffer;
    }
    
    private ref BufferEntry GetBufferEntry(uint bufferId)
    {
        if (bufferId < bufferEntries.Length) {
            ref var entry = ref bufferEntries[bufferId];
            if (entry.bufferSegments == null) {
                entry = new BufferEntry(bufferId);
            }
            return ref entry;
        }
        return ref ResizeBufferEntries(bufferId);
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private ref BufferEntry ResizeBufferEntries(uint bufferId)
    {
        var newEntries  = new BufferEntry[bufferId + 1];
        var entries     = bufferEntries;
        Array.Copy(entries, newEntries, entries.Length);
        bufferEntries  = newEntries;
        newEntries[bufferId] = new BufferEntry(bufferId);
        return ref newEntries[bufferId];
    }
    
    public void TrackWrite<T>(in Buffer<T> buffer) where T : unmanaged
    {
        if (false) requestedRanges.Add(new BufferRange(buffer.GpuBuffer.DeviceBufferId, buffer.Offset, buffer.Length));
    }

    

    internal CommandRecorder(WgpuDevice device) {
        this.device         = device;
        slotSize            = device.SlotSize;
        globalUniformPool   = device.globalUniformPool.handle;
        stagingBuffer       = new byte[device.SlotSize];
    }
    
    // The recorder provides / owns the Encoder
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WgpuEncoder GetEncoder(ReadOnlySpan<byte> encoderLabel) {
        var encoder     = device.CreateEncoder(this, encoderLabel); 
        currentEncoder  = encoder.handle;
        return encoder;
    }
    
    public BindGroupEntry AsUniformEntry<T>(int binding, T value) where T : unmanaged
    {
        uint size           = (uint)sizeof(T);
        uint alignedOffset  = (uniformOffset + 255) & ~255u; // WebGPU requires Uniform offset must by 256 byte aligned
        
        if (alignedOffset + size > slotSize) {
            ThrowUniformSlotOverflow();
        }
        // write directly to stagingBuffer
        fixed (byte* pDest = &stagingBuffer[alignedOffset]) {
            *(T*)pDest = value;
        }
        uniformOffset = alignedOffset + size;

        return new BindGroupEntry {
            binding = (uint)binding,
            buffer  = globalUniformPool,
            offset  = alignedOffset,
            size    = size
        };
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)][StackTraceHidden][DoesNotReturn]
    private void ThrowUniformSlotOverflow() {
        throw new IndexOutOfRangeException($"Uniform slot overflow. slotSize: {slotSize}.");
    } 
    
    public void Finish(WgpuEncoder encoder, ReadOnlySpan<byte> commandBufferLabel)
    {
        if (uniformOffset > 0) {
            fixed (byte* pData = stagingBuffer) {
                device.WriteBuffer(device.globalUniformPool, 0, pData, uniformOffset);
            }
        }
        // TODO  Ultimate performance upgrade
        // If batch upload gets a bottleneck globalUniformPool must be created as "Persistent Mapped Buffer" (Host Visible).
        // This eliminates the WriteBuffer() call entirely because AsUniformEntry<> will than write directly in GPU memory.
        // This requires WGPU Buffer Map/Unmap Lifecycle Management
        fixed (byte* labelPtr = commandBufferLabel) {
            var descriptor = new CommandBufferDescriptor { label = WgpuUtils.FromPtrSpan(labelPtr, commandBufferLabel) };
            commandBuffer  = wgpuCommandEncoderFinish(encoder.handle, &descriptor);
        }
        if (currentEncoder != null) {
            wgpuCommandEncoderRelease(currentEncoder);
            currentEncoder = null;
        }
        if (device.errorHandler.errorType != ErrorType.NoError) {
            // device.ReturnTask(this);       // TASK_TAG
            device.errorHandler.ThrowException(); // e.g. ErrorType.Validation : Attempted to use Buffer with 'gpuOutput' label with conflicting usages. ...
        }
        commandBuffers.Add((nint)commandBuffer);
    }
    
    public WgpuBindGroup CreateBindGroup(WgpuBindGroupLayout layout, BindGroupEntry bindEntry, ReadOnlySpan<byte> groupLabel)
    {
        fixed(byte* labelPtr = groupLabel) {
            var descriptor = new BindGroupDescriptor {
                label       = WgpuUtils.FromPtrSpan(labelPtr, groupLabel), 
                layout      = layout.handle,
                entryCount  = 1,
                entries     = &bindEntry
            };
            var handle = wgpuDeviceCreateBindGroup(device.DevicePtr, &descriptor);
            createdBindGroups[createdBindGroupsCount++] = (nint)handle;
            return new WgpuBindGroup(handle);
        }
    }
    
    public WgpuBindGroup CreateBindGroup(WgpuBindGroupLayout layout, ReadOnlySpan<BindGroupEntry> bindEntries, ReadOnlySpan<byte> groupLabel)
    {
        fixed(byte*             labelPtr        = groupLabel)
        fixed(BindGroupEntry*   nativeEntryPtr  = bindEntries) {
            var descriptor = new BindGroupDescriptor {
                label       = WgpuUtils.FromPtrSpan(labelPtr, groupLabel), 
                layout      = layout.handle,
                entryCount  = (uint)bindEntries.Length,
                entries     = nativeEntryPtr
            };
            var handle = wgpuDeviceCreateBindGroup(device.DevicePtr, &descriptor);
            createdBindGroups[createdBindGroupsCount++] = (nint)handle;
            return new WgpuBindGroup(handle);
        }
    }
    
    internal void Reset()     // TODO remove
    {
        for (int n = 0; n < createdBindGroupsCount; n++) {
            wgpuBindGroupRelease((BindGroup*)createdBindGroups[n]);
        }
        createdBindGroupsCount = 0;
        
        var bufferHandler = commandBuffer;
        if (bufferHandler != null) {
            // Note: In case wgpuCommandEncoderFinish() detected a validation error
            //       releasing the handle will not decrement GpuHandleDiff.CommandBuffers
            wgpuCommandBufferRelease(bufferHandler);
            commandBuffer = null;
        }
        uniformOffset = 0; // reset local uniform cursor
        
        Dispose();
        
        isCompleted 	= false;
        isSubmitted 	= false;
    }
    
    public void Dispose()
    {
        ClosePass();
        if (currentEncoder != null) {
            wgpuCommandEncoderRelease(currentEncoder);
            currentEncoder = null;
        }
    }
    
    internal void ClosePass() {
        if (currentPass != null) {
            wgpuComputePassEncoderEnd(currentPass);
            wgpuComputePassEncoderRelease(currentPass);
            currentPass = null;
        }
    }
    
    private readonly    List<BufferRange>   tempRanges    = new();
    private readonly    List<BufferData>    activeBuffers = new ();
    
    internal void Download()
    {
        foreach (var range in requestedRanges) {
            bufferEntries[range.bufferId].requestedRanges.Add(range);
        }
        
        var encoder = wgpuDeviceCreateCommandEncoder(device.DevicePtr, null);
        activeBuffers.Clear();
        var bufferMap = device.bufferMap;

        foreach (var bufferEntry in bufferEntries)
        {
            var ranges = bufferEntry.requestedRanges;
            if (ranges == null || ranges.Count == 0) {
                continue;
            }
            var buffer              = bufferMap[(int)bufferEntry.bufferId].GetBufferData();
            buffer.requestedRanges  = ranges;
            activeBuffers.Add(buffer);

            var  optimizedRanges = BufferRange.GetOptimizedRanges(ranges, tempRanges);
            uint elementSize     = (uint)buffer.elementSize;
            foreach (var range in optimizedRanges)
            {
                uint byteOffset = (uint)range.start  * elementSize;
                uint byteSize   = (uint)range.length * elementSize;

                // GPU internal copy from fast compute memory in persistent stating buffer
                wgpuCommandEncoderCopyBufferToBuffer(
                    encoder,
                    buffer.storageHandle,   // source: GPU Storage [Storage]
                    byteOffset,
                    buffer.stagingHandle,   // target: persistant Readback [MapRead]
                    byteOffset,
                    byteSize
                );
            }
        }

        // finish commands and send to GPU queue
        var sendCommandBuffer = wgpuCommandEncoderFinish(encoder, null);
        wgpuQueueSubmit(device.QueuePtr, 1, &sendCommandBuffer);
        
        wgpuCommandBufferRelease(sendCommandBuffer);
        wgpuCommandEncoderRelease(encoder);

        int remainingMaps = activeBuffers.Count; // decremented to 0 if all wgpuBufferMapAsync are finished
        
        foreach (var buffer in activeBuffers)
        {
            uint totalBufferSizeInBytes = (uint)(buffer.length * buffer.elementSize);
            
            // simply map the whole memory instead of the smaller ranges
            var callbackInfo = new BufferMapCallbackInfo {
                mode        = CallbackMode.AllowProcessEvents,
                callback    = &BufferMap_callback,
                userdata1   = &remainingMaps
            };
            wgpuBufferMapAsync(buffer.stagingHandle, (ulong)MapMode.Read, 0, totalBufferSizeInBytes, callbackInfo);
        }
        // the only single CPU-Stall: wait until all buffers are mapped
        while (Thread.VolatileRead(ref remainingMaps) > 0) {
            // wgpuDeviceTick(NativePtr);
            wgpuInstanceProcessEvents(device.instance);
        }
        // direct CPU -> CPU transfer staging memory -> host memory
        foreach (var buffer in activeBuffers)
        {
            uint totalBufferSizeInBytes = (uint)(buffer.length * buffer.elementSize);
            void* pMapped = wgpuBufferGetMappedRange(buffer.stagingHandle, 0, totalBufferSizeInBytes);
            
            var wgpuBuffer = bufferMap[buffer.bufferId];
            wgpuBuffer.ExecuteCpuCopy(pMapped, buffer.requestedRanges);     // copy staging memory to host memory
            
            wgpuBufferUnmap(buffer.stagingHandle);                          // unmap so CPU is able to access
            buffer.requestedRanges.Clear();
        }
        activeBuffers.Clear();
    }
    
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void BufferMap_callback(MapAsyncStatus status, StringView message, void* userdata1, void* userdata2) {
        if (userdata1== null) return;
        var remainingMaps = (int*)userdata1;
        Interlocked.Decrement(ref *remainingMaps);
    }
}
