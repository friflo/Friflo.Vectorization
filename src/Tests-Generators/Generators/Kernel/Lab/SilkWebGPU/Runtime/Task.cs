// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;
using Webgpu = Silk.NET.WebGPU.WebGPU;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Kernel.SilkWebGPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed unsafe class SilkTask : IDisposable
{
    private  readonly   SilkDevice          device;
    internal readonly   Webgpu              wgpu;
    private             CommandEncoder*     currentEncoder;             // SilkTask owns CommandEncoder* and ensures release
    internal            ComputePassEncoder* currentPass;                // SilkTask owns ComputePassEncoder* and ensures release
    // Pre-allocated to avoid heap growth during the hot loop.
    // 4 slots cover the standard WebGPU maxBindGroups limit for most tasks, ensuring a zero-allocation steady state.
    private readonly    List<nint>          createdBindGroups = new(4); // SilkTask owns all created BindGroup* and ensures release  
    internal            CommandBuffer*      commandBuffer;
    
    private readonly    int                 taskIndex;
    private readonly    uint                uniformBase;                // base position in pool slice - used as a ring buffer
    private             uint                uniformOffset;             	// cursor in pool slice used as a ring buffer
    private readonly    byte[]              stagingBuffer;              // CPU-cache for uniform buffer
    private readonly    int                 slotSize;
    private readonly    Buffer*             globalUniformPool;
    
    internal            bool                IsSubmitted     { get; private set; }   // TODO remove
    internal            bool                IsCompleted     { get; private set; }   // TODO remove
    
    
    // A simple state flag for the scheduler

    

    internal SilkTask(SilkDevice device, int taskIndex) {
        this.device         = device;
        wgpu                = device.wgpu;
        slotSize            = device.UniformBufferSize;
        globalUniformPool   = device.globalUniformPool.handle;
        this.taskIndex      = taskIndex;
        uniformBase         = (uint)(taskIndex * slotSize);
        stagingBuffer       = new byte[device.UniformBufferSize];
    }
    
    // The task provides / owns the Encoder
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SilkEncoder GetEncoder(ReadOnlySpan<byte> encoderLabel) {
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
        uint absoluteOffset = uniformBase + alignedOffset;
        
        uniformOffset = alignedOffset + size;

        return new BindGroupEntry {
            Binding = (uint)binding,
            Buffer  = globalUniformPool,
            Offset  = absoluteOffset,
            Size    = size
        };
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)][StackTraceHidden][DoesNotReturn]
    private void ThrowUniformSlotOverflow() {
        throw new IndexOutOfRangeException($"Uniform slot overflow. taskIndex: {taskIndex} slotSize: {slotSize}.");
    } 
    
    public void Finish(SilkEncoder encoder, ReadOnlySpan<byte> commandBufferLabel)
    {
        if (uniformOffset > 0) {
            fixed (byte* pData = stagingBuffer) {
                device.WriteBuffer(device.globalUniformPool, uniformBase, pData, uniformOffset);
            }
        }
        // TODO  Ultimate performance upgrade
        // If batch upload gets a bottleneck globalUniformPool must be created as "Persistent Mapped Buffer" (Host Visible).
        // This eliminates the WriteBuffer() call entirely because AsUniformEntry<> will than write directly in GPU memory.
        // This requires WGPU Buffer Map/Unmap Lifecycle Management
        fixed (byte* labelPtr = commandBufferLabel) {
            var descriptor = new CommandBufferDescriptor { Label = labelPtr };
            commandBuffer  = wgpu.CommandEncoderFinish(encoder.handle, &descriptor);
        }
        if (currentEncoder != null) {
            wgpu.CommandEncoderRelease(currentEncoder);
            currentEncoder = null;
        }
    }
    
    public SilkBindGroup CreateBindGroup(SilkBindGroupLayout layout, BindGroupEntry bindEntry, ReadOnlySpan<byte> groupLabel)
    {
        fixed(byte* labelPtr = groupLabel) {
            var descriptor = new BindGroupDescriptor {
                Label       = labelPtr, 
                Layout      = layout.handle,
                EntryCount  = 1,
                Entries     = &bindEntry
            };
            var handle = wgpu.DeviceCreateBindGroup(device.DevicePtr, &descriptor);
            createdBindGroups.Add((nint)handle);
            return new SilkBindGroup(handle);
        }
    }
    
    public SilkBindGroup CreateBindGroup(SilkBindGroupLayout layout, Span<BindGroupEntry> bindEntries, ReadOnlySpan<byte> groupLabel)
    {
        fixed(byte*             labelPtr        = groupLabel)
        fixed(BindGroupEntry*   nativeEntryPtr  = bindEntries) {
            var descriptor = new BindGroupDescriptor {
                Label       = labelPtr, 
                Layout      = layout.handle,
                EntryCount  = (uint)bindEntries.Length,
                Entries     = nativeEntryPtr
            };
            var handle = wgpu.DeviceCreateBindGroup(device.DevicePtr, &descriptor);
            createdBindGroups.Add((nint)handle);
            return new SilkBindGroup(handle);
        }
    }
    
    internal void Reset()
    {
        foreach (var ptr in createdBindGroups) {
            wgpu.BindGroupRelease((BindGroup*)ptr);
        }
        createdBindGroups.Clear();
        
        var bufferHandler = commandBuffer;
        if (bufferHandler != null) {
            wgpu.CommandBufferRelease(bufferHandler);
            commandBuffer = null;
        }
        uniformOffset = 0; // reset local uniform cursor
        
        Dispose();
        
        IsCompleted 	= false;
        IsSubmitted 	= false;
    }

    public void Dispose()
    {
        ClosePass();
        if (currentEncoder != null) {
            wgpu.CommandEncoderRelease(currentEncoder);
            currentEncoder = null;
        }
    }
    
    internal void ClosePass() {
        if (currentPass != null) {
            wgpu.ComputePassEncoderEnd(currentPass);
            wgpu.ComputePassEncoderRelease(currentPass);
            currentPass = null;
        }
    }
    
    public void SetSubmitted(bool submitted) {
        IsSubmitted = submitted;
    }

    public void SetCompleted(bool completed) {
        IsCompleted = completed;
    }
}
