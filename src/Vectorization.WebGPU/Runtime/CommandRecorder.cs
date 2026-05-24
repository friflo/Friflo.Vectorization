// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

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
    internal readonly   List<BufferRange>   requestedRanges = new();
    internal readonly   List<nint>          commandBuffers  = new();
    
    internal            bool                isSubmitted;        // TODO remove
    internal            bool                isCompleted;        // TODO remove
    
    public GpuBuffer<T> RequireRead<T>(in InBuffer<T> buffer) where T : unmanaged
    {
        return buffer.GpuBuffer;
    }
    
    public GpuBuffer<T> RequireReadWrite<T>(in Buffer<T> buffer) where T : unmanaged
    {
        return buffer.GpuBuffer;
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
}
