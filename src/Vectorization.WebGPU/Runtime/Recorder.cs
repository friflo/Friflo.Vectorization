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

// ReSharper disable InvertIf
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed unsafe partial class CommandRecorder : IDisposable
{
    private  readonly   WgpuDevice          device;
    private             WgpuEncoder         currentEncoder;
    private             ComputePassEncoder* currentPass;
    internal            bool                enablePassBatching  = false;
    internal            int                 renderPassCount;
    
    private  readonly   List<WgpuBindGroup> createdBindGroups   = [];   // TODO can use array
    private             CommandBuffer*      commandBuffer;
    
    private             uint                uniformOffset;              // cursor in pool slice used as a ring buffer
    private  readonly   byte[]              stagingBuffer;              // CPU-cache for uniform buffer
    private  readonly   int                 slotSize;
    private  readonly   Buffer*             globalUniformPool;

    internal readonly   List<nint>          commandBuffers      = [];   // only used if enablePassBatching == false
    private             int                 kernelSeq;
    private             int                 kernelId            = -1;
    private             bool                createNewPass;
    

    public   override   string              ToString()          => $"newPass: {createNewPass}";

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
    
    public void TrackWrite<T>(in Buffer<T> buffer) where T : unmanaged
    {
        requestedRanges.Add(new BufferRange(buffer.GpuBuffer.DeviceBufferId, buffer.Offset, buffer.Length));
    }
    

    internal CommandRecorder(WgpuDevice device) {
        this.device         = device;
        slotSize            = device.SlotSize;
        globalUniformPool   = device.globalUniformPool.handle;
        stagingBuffer       = new byte[device.SlotSize];
    }
    
    // The recorder provides / owns the Encoder
    public WgpuComputePass BeginComputePass(ReadOnlySpan<byte> passLabel)
    {
        if (enablePassBatching && !createNewPass) {
            return new WgpuComputePass(this, currentPass, passLabel);  
        }
        renderPassCount++;
        fixed (byte* labelPtr = passLabel)
        {
            var label       = WgpuUtils.FromPtrSpan(labelPtr, passLabel);
            currentEncoder  = device.CreateEncoder(label);
            var desc        = new ComputePassDescriptor { label = label };
            currentPass     = wgpuCommandEncoderBeginComputePass(currentEncoder.handle, &desc);
            return new WgpuComputePass(this, currentPass, passLabel);
        }
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
    
    /// Called only if <see cref="enablePassBatching"/> == false
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void Finish(ReadOnlySpan<byte> commandBufferLabel)
    {
        renderPassCount = 0;
        foreach (var group in createdBindGroups) {
            wgpuBindGroupRelease(group.handle);
        }
        createdBindGroups.Clear();
        ClosePass();
        
        if (uniformOffset > 0) {
            fixed (byte* pData = stagingBuffer) {
                device.WriteBuffer(device.globalUniformPool, 0, pData, uniformOffset);
            }
        }
        // TODO  Ultimate performance upgrade
        // If batch upload gets a bottleneck globalUniformPool must be created as "Persistent Mapped Buffer" (Host Visible).
        // This eliminates the WriteBuffer() call entirely because AsUniformEntry<> will than write directly in GPU memory.
        // This requires WGPU Buffer Map/Unmap Lifecycle Management
        
        var encoderHandle = currentEncoder.handle;
        fixed (byte* labelPtr = commandBufferLabel) {
            var descriptor = new CommandBufferDescriptor { label = WgpuUtils.FromPtrSpan(labelPtr, commandBufferLabel) };
            commandBuffer  = wgpuCommandEncoderFinish(encoderHandle, &descriptor);
        }
        if (encoderHandle != null) {
            wgpuCommandEncoderRelease(encoderHandle);
            currentEncoder = default;
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
            var group = new WgpuBindGroup(handle); 
            createdBindGroups.Add(group);
            return group;
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
            var group = new WgpuBindGroup(handle); 
            createdBindGroups.Add(group);
            return group;
        }
    }
#if DEBUG
    internal void Reset()     // TODO remove
    {
        foreach (var group in createdBindGroups) {
            wgpuBindGroupRelease(group.handle);
        }
        createdBindGroups.Clear();
        
        var bufferHandler = commandBuffer;
        if (bufferHandler != null) {
            // Note: In case wgpuCommandEncoderFinish() detected a validation error
            //       releasing the handle will not decrement GpuHandleDiff.CommandBuffers
            wgpuCommandBufferRelease(bufferHandler);
            commandBuffer = null;
        }
        uniformOffset = 0; // reset local uniform cursor
        
        Dispose();
    }
#endif
    
    public void Dispose()
    {
        ClosePass();
        if (currentEncoder.handle != null) {
            wgpuCommandEncoderRelease(currentEncoder.handle);
            currentEncoder = default;
        }
    }
    
    private void ClosePass() {
        if (currentPass != null) {
            wgpuComputePassEncoderEnd(currentPass);
            wgpuComputePassEncoderRelease(currentPass);
            currentPass = null;
        }
    }
 }
