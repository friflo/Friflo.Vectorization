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
    internal            ulong                   lastBindGroup0_hash;
    internal            ComputePipeline*        lastPipelineHandle;
    
    private  readonly   List<WgpuBindGroup>     createdBindGroups   = [];   // TODO can use array
    private             WgpuCommandBuffer       commandBuffer;
    
    private             WgpuBindGroup[]         uniformBindGroups   = [];
    private             WgpuBuffer<byte>        uniformBuffer;
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
        if (enableTraces) {
            AddKernelTrace(TraceType.Kernel, kernelId);
        }
        fixed (byte* labelPtr = passLabel)
        {
            var label       = WgpuUtils.FromPtrSpan(labelPtr, passLabel);
            var desc        = new ComputePassDescriptor { label = label };
            currentPass     = wgpuCommandEncoderBeginComputePass(currentEncoder.handle, &desc);
            return new WgpuComputePass(this, currentPass);
        }
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void FinishPass()
    {
        if (currentPass== null) {
            return;
        }
        lastBindGroup0_hash =  0;
        lastPipelineHandle  =  null;
        kernelSeq           =  0;
        kernelId            = -1;
        foreach (var segmentMap in clearSegmentMaps) {
            segmentMap.Clear();
        }
        clearSegmentMaps.Clear();
        
        foreach (var group in createdBindGroups) {
            wgpuBindGroupRelease(group.handle);
        }
        createdBindGroups.Clear();
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
        uniformOffset       =  0;
        
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
        if (currentPass != null) {
            wgpuComputePassEncoderEnd(currentPass);
            wgpuComputePassEncoderRelease(currentPass);
            currentPass = null;
        }
    }
    
    internal WgpuBindGroup GetUniformBindGroup(ref WgpuComputeEffect effect, uint uniformSize, ReadOnlySpan<byte> groupLabel)
    {
        var bindGroups = uniformBindGroups;
        
        if (effect.kernelId < bindGroups.Length) {
            var bindGroup = bindGroups[effect.kernelId];
            if (bindGroup.handle != null) {
                return bindGroup;
            }
        }
        return CreateUniformBindGroup(ref effect, uniformSize, groupLabel);
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private WgpuBindGroup CreateUniformBindGroup(ref WgpuComputeEffect effect, uint uniformSize, ReadOnlySpan<byte> groupLabel)
    {
        var entry = new BindGroupEntry {
            binding = 0,
            buffer  = uniformBuffer.handle,
            offset  = 0,
            size    = uniformSize
        };
        fixed(byte* labelPtr = groupLabel) {
            var desc = new BindGroupDescriptor {
                label       = WgpuUtils.FromPtrSpan(labelPtr, groupLabel),
                layout      = effect.uniformLayout.handle,
                entryCount  = 1,
                entries     = &entry
            };
            var groupHandle = wgpuDeviceCreateBindGroup(device.DevicePtr, &desc);
            var bindGroup   = new WgpuBindGroup(groupHandle);
            
            var groups = uniformBindGroups;
            if (effect.kernelId >= groups.Length) {
                var newGroups = new WgpuBindGroup[Math.Max(groups.Length * 2, effect.kernelId + 1)];
                Array.Copy(groups, 0, newGroups, 0, groups.Length);
                groups = uniformBindGroups = newGroups;
            }
            return groups[effect.kernelId] = bindGroup;
        }
    }
}

