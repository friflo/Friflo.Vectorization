// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
// ReSharper disable InvertIf
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed unsafe partial class CommandRecorder : PipelineContext
{
    private  readonly   WgpuDevice              device;
    internal            WgpuEncoder             currentEncoder;
    private             ComputePassEncoder*     currentPass;
    internal            PassBatching            enablePassBatching 	= PassBatching.HazardDriven;
    internal            ComputePipeline*        lastPipelineHandle;
    internal            BindGroup*              lastBufferBindGroup;
    
    private             WgpuCommandBuffer       commandBuffer;
    private  readonly   BindGroupEntry[]        bindGroupEntries = new  BindGroupEntry[1000];
    private             int                     bindGroupEntriesCount;
    
    private             WgpuBuffer<byte>        uniformBuffer;
    internal readonly   uint[]                  uniformOffsets      = new uint[100];
    internal            uint                    uniformOffsetsCount;
    internal            uint                    uniformOffset;              // cursor in pool slice used as a ring buffer
    internal const      uint                    UniformAlignment    = 256;
    private  readonly   int                     uniformBufferSize;
    internal readonly   byte[]                  stagingBuffer;              // CPU-cache for uniform buffer

    private             int                     kernelSeq;
    private             int                     kernelId            = -1;
    internal            bool                    createNewPass;
    private  readonly   List<SegmentMap>        clearSegmentMaps    = new (10);
    
    public              WgpuDevice              Device => device;
    

    public   override   string                  ToString()          => $"newPass: {createNewPass}";

    public void Init(int id, ReadOnlySpan<byte> encoderLabel)
    {
        if (currentEncoder.handle == null) {
            fixed (byte* labelPtr = encoderLabel) {
                var label       = WgpuUtils.FromPtrSpan(labelPtr, encoderLabel);
                currentEncoder  = device.CreateEncoder(label);
            }
        }
        traceNewKernel  = kernelId != id;
        createNewPass   = kernelSeq == 0; // kernelId != id;
        kernelId        = id;
        kernelSeq++;
        pipelineStats.Calls++;
        
        var metrics = kernelMetrics;
        if (id < metrics.Length) {
            metrics[id].Calls++;
        } else {
            ResizeAndIncrementMetric(id);
        }
    }
    
    [StackTraceHidden]
    public void RequireRead<T>(in InBuffer<T> buffer) where T : unmanaged
    {
        var gpuBuffer   = buffer.Buffer;
        WriteBufferRanges(gpuBuffer.DeviceBufferId);
        
        var segments    = GetBufferSegments(gpuBuffer.DeviceBufferId);
        createNewPass  |= AddRead(segments, buffer.Offset, buffer.Length, kernelId, kernelSeq, gpuBuffer.Label);
    }
    
    [StackTraceHidden]
    public void RequireReadWrite<T>(in InOutBuffer<T> buffer) where T : unmanaged
    {
        var gpuBuffer   = buffer.Buffer;
        WriteBufferRanges(gpuBuffer.DeviceBufferId);
        
        var segments    = GetBufferSegments(gpuBuffer.DeviceBufferId);
        createNewPass  |= AddReadWrite(segments, buffer.Offset, buffer.Length, kernelId, kernelSeq, gpuBuffer.Label);
    }
    
    internal CommandRecorder(WgpuDevice device) : base(device) 
    {
        this.device         = device;
        uniformBufferSize   = device.UniformBufferSize;
        commandList         = device.commandListPool.Fetch();
        stagingBuffer       = new byte[uniformBufferSize];
    }
    
    // The recorder provides / owns the Encoder
    public WgpuComputePass BeginComputePass(ReadOnlySpan<byte> passLabel)
    {
        if (enablePassBatching == PassBatching.HazardDriven && !createNewPass) {
            if (enableTraces) {
                UpdateKernelTrace();
            }
            return new WgpuComputePass(this, currentPass);
        }
        pipelineStats.Passes++;
        kernelMetrics[kernelId].Passes++;

        if (currentPass != null) {
            wgpuComputePassEncoderEnd(currentPass);
        }
        fixed (byte* labelPtr = passLabel)
        {
            var label       = WgpuUtils.FromPtrSpan(labelPtr, passLabel);
            var desc        = new ComputePassDescriptor { label = label };
            currentPass     = wgpuCommandEncoderBeginComputePass(currentEncoder.handle, &desc);
        }
        if (enableTraces) {
            AddKernelTrace(kernelId);
        }
        return new WgpuComputePass(this, currentPass);
    }
    
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void FinishPass()
    {
        if (currentPass== null) {
            return;
        }
        Reset();
    }
    
    internal void Reset()
    {
        lastPipelineHandle  =  null;
        kernelSeq           =  0;
        kernelId            = -1;
        foreach (var segmentMap in clearSegmentMaps) {
            segmentMap.Clear();
        }
        clearSegmentMaps.Clear();
        
        ClosePass();
        
        if (uniformOffset > 0) {
            fixed (byte* pData = stagingBuffer) {
                wgpuQueueWriteBuffer(device.QueuePtr, uniformBuffer.handle, 0, pData, uniformOffset);
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void FinishEncoder(ReadOnlySpan<byte> commandBufferLabel)
    {
        var encoderHandle = currentEncoder.handle;
        if (encoderHandle == null) {
            return;
        }
        uniformOffset       = 0;
        uniformOffsetsCount = 0;
        
        // TODO  Ultimate performance upgrade
        // If batch upload gets a bottleneck globalUniformPool must be created as "Persistent Mapped Buffer" (Host Visible).
        // This eliminates the WriteBuffer() call entirely because AsUniformEntry<> will than write directly in GPU memory.
        // This requires WGPU Buffer Map/Unmap Lifecycle Management
        
        fixed (byte* labelPtr = commandBufferLabel) {
            var descriptor = new CommandBufferDescriptor { label = WgpuUtils.FromPtrSpan(labelPtr, commandBufferLabel) };
            var cbHandle   = wgpuCommandEncoderFinish(encoderHandle, &descriptor);
            commandBuffer  = new WgpuCommandBuffer(cbHandle); 
        }
        wgpuCommandEncoderRelease(encoderHandle);
        
        currentEncoder = default;

        if (device.errorHandler.errorType != ErrorType.NoError) {
            // device.ReturnTask(this);       // TASK_TAG
            device.errorHandler.ThrowException(); // e.g. ErrorType.Validation : Attempted to use Buffer with 'gpuOutput' label with conflicting usages. ...
        }
        commandList.commands.Add(commandBuffer);
    }

    // ---------------- add BindGroupEntry*() / CreateBindGroup() ----------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindGroupEntryUniform<T>() where T : unmanaged
    {
        uint alignedSize = ((uint)sizeof(T) + (UniformAlignment - 1)) & ~(UniformAlignment - 1);
        BindGroupEntryUniformInternal(alignedSize);
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BindGroupEntryUniformInternal(uint alignedSize)
    {
        bindGroupEntries[bindGroupEntriesCount] = new BindGroupEntry {
            binding = (uint)bindGroupEntriesCount++,
            buffer  = uniformBuffer.handle,
            offset  = 0,
            size    = alignedSize
        };
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void BindGroupEntrySampler(GpuSampler sampler)
    {
        bindGroupEntries[bindGroupEntriesCount] = new BindGroupEntry {
            binding = (uint)bindGroupEntriesCount++,
            sampler = sampler.handle
        };
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void BindGroupEntryTexture(in GpuTextureView textureView)
    {
        bindGroupEntries[bindGroupEntriesCount] = new BindGroupEntry {
            binding     = (uint)bindGroupEntriesCount++,
            textureView = textureView.handle
        };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindGroupEntryBuffer<T>(GpuBuffer<T> buffer) where T : unmanaged
    {
        BindGroupEntryBufferInternal(buffer.NativeHandle, Unsafe.SizeOf<T>() * buffer.Length);
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BindGroupEntryBufferInternal(nint buffer, int size)
    {
        bindGroupEntries[bindGroupEntriesCount] = new BindGroupEntry {
            binding = (uint)bindGroupEntriesCount++,
            buffer  = (Buffer*)buffer,
            offset  = 0,
            size    = (uint)size
        };
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public WgpuBindGroup CreateBindGroup(WgpuBindGroupLayout layout, ReadOnlySpan<byte> groupLabel)
    {
        fixed(byte*             labelPtr        = groupLabel)
        fixed(BindGroupEntry*   nativeEntryPtr  = bindGroupEntries) {
            var descriptor = new BindGroupDescriptor {
                label       = WgpuUtils.FromPtrSpan(labelPtr, groupLabel), 
                layout      = layout.handle,
                entryCount  = (uint)bindGroupEntriesCount,
                entries     = nativeEntryPtr
            };
            bindGroupEntriesCount = 0;
            var handle = wgpuDeviceCreateBindGroup(device.DevicePtr, &descriptor);
            return new WgpuBindGroup(handle);
        }
    }
    
    // TODO REMOVE
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal BindGroupEntry CreateUniformBindGroupEntry<T>(int binding) where T : unmanaged
    {
        uint alignedSize    = ((uint)sizeof(T) + (UniformAlignment - 1)) & ~(UniformAlignment - 1);
        return new BindGroupEntry {
            binding = (uint)binding,
            buffer  = uniformBuffer.handle,
            offset  = 0,
            size    = alignedSize
        };
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal WgpuBindGroup CreateBindGroupInternal(WgpuBindGroupLayout layout, BindGroupEntry entry, ReadOnlySpan<byte> groupLabel)
    {
        fixed(byte*             labelPtr        = groupLabel) {
            var descriptor = new BindGroupDescriptor {
                label       = WgpuUtils.FromPtrSpan(labelPtr, groupLabel), 
                layout      = layout.handle,
                entryCount  = 1,
                entries     = &entry
            };
            var handle = wgpuDeviceCreateBindGroup(device.DevicePtr, &descriptor);
            var group = new WgpuBindGroup(handle); 
            return group;
        }
    }
        
    public override void Dispose()
    {
        ClosePass();
        if (currentEncoder.handle != null) {
            wgpuCommandEncoderRelease(currentEncoder.handle);
            currentEncoder = default;
        }
        base.Dispose();
    }
    
    private void ClosePass()
    {
        lastBufferBindGroup =  null;
        if (currentPass != null) {
            wgpuComputePassEncoderEnd(currentPass);
            wgpuComputePassEncoderRelease(currentPass);
            currentPass = null;
        }
    }
}

