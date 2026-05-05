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

// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.Vectorization.GPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed unsafe class GpuTask : IDisposable
{
    private  readonly   GpuDevice           device;
    internal readonly   WebGPU              wgpu;
    private             CommandEncoder*     currentEncoder;             // GpuTask owns CommandEncoder* and ensures release
    internal            ComputePassEncoder* currentPass;                // GpuTask owns ComputePassEncoder* and ensures release
    // Pre-allocated to avoid heap growth during the hot loop.
    // 4 slots cover the standard WebGPU maxBindGroups limit for most tasks, ensuring a zero-allocation steady state.
    private readonly    List<nint>          createdBindGroups = new(4); // GpuTask owns all created BindGroup* and ensures release  
    internal            CommandBuffer*      commandBuffer;
    private readonly    List<GpuTask>       dependencies = new();       // Tasks that MUST finish before this one starts
    
    private readonly    int                 taskIndex;
    private readonly    uint                uniformBase;                // base position in pool slice - used as a ring buffer
    private             uint                uniformOffset;             	// cursor in pool slice used as a ring buffer
    private readonly    byte[]              stagingBuffer;              // CPU-cache for uniform buffer
    private readonly    int                 slotSize;
    private readonly    Buffer*             globalUniformPool;
    
    
    // A simple state flag for the scheduler
    public              bool                IsSubmitted     { get; internal set; }
    public              bool                IsCompleted     { get; internal set; }
    

    internal GpuTask(GpuDevice device, int taskIndex) {
        this.device         = device;
        wgpu                = device.wgpu;
        slotSize            = device.slotSize;
        globalUniformPool   = device.globalUniformPool.handle;
        this.taskIndex      = taskIndex;
        uniformBase         = (uint)(taskIndex * slotSize);
        stagingBuffer       = new byte[device.slotSize];
    }
    
    // The task provides / owns the Encoder
    public GpuEncoder GetEncoder(ReadOnlySpan<byte> encoderLabel) {
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
    
    public void Finish(GpuEncoder encoder, ReadOnlySpan<byte> commandBufferLabel)
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
    
    public GpuBindGroup CreateBindGroup(GpuBindGroupLayout layout, Span<BindGroupEntry> bindEntries, ReadOnlySpan<byte> groupLabel)
    {
        fixed(byte*             labelPtr        = groupLabel)
        fixed(BindGroupEntry*   nativeEntryPtr  = bindEntries)
        {
            var descriptor = new BindGroupDescriptor {
                Label       = labelPtr, 
                Layout      = layout.handle,
                EntryCount  = (uint)bindEntries.Length,
                Entries     = nativeEntryPtr
            };
            var handle = wgpu.DeviceCreateBindGroup(device.DevicePtr, &descriptor);
            createdBindGroups.Add((nint)handle);
            return new GpuBindGroup(handle);
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
        
        dependencies.Clear();
    }

    public void AddDependency(GpuTask predecessor) {
        if (predecessor == this) return; // Prevent brain-loop
        if (!dependencies.Contains(predecessor))
        {
            dependencies.Add(predecessor);
        }
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
}

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct GpuEncoder
{
    private  readonly   GpuTask         task;
    internal readonly   CommandEncoder* handle;
    
    internal GpuEncoder(GpuTask task, CommandEncoder* handle) {
        this.task   = task;
        this.handle = handle;
    }
    
    // --- ComputePass methods
    public GpuComputePass BeginComputePass(ReadOnlySpan<byte> passLabel)
    {
        fixed (byte* labelPtr = passLabel)
        {
            var desc            = new ComputePassDescriptor { Label = labelPtr };
            var passHandle      = task.wgpu.CommandEncoderBeginComputePass(handle, &desc);
            task.currentPass    = passHandle;
            return new GpuComputePass(task, passHandle);
        }
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct GpuComputePass : IDisposable {
    private readonly    GpuTask             task;
    private readonly    ComputePassEncoder* handle;
    
    public GpuComputePass(GpuTask task, ComputePassEncoder* handle) {
        this.task   = task;
        this.handle = handle;
    }
    
    public void Dispose() {
        End();
    }

    public void SetPipeline(GpuComputePipeline pipeline) {
        task.wgpu.ComputePassEncoderSetPipeline(handle, pipeline.handle);
    }
    
    public void DispatchWorkgroups(int workgroupCountX, int workgroupCountY, int workgroupCountZ) {
        task.wgpu.ComputePassEncoderDispatchWorkgroups(
            handle, 
            (uint)workgroupCountX, 
            (uint)workgroupCountY, 
            (uint)workgroupCountZ
        );
    }

    public void End() {
        task.ClosePass(); 
    }

    public void SetBindGroup(int groupIndex, GpuBindGroup bindGroup)
    {
        // 4th and 5th parameter are for dynamic offsets (0/null)
        task.wgpu.ComputePassEncoderSetBindGroup(handle, (uint)groupIndex, bindGroup.handle, 0, null);
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct GpuBindGroup
{
    internal readonly BindGroup* handle;
    
    internal GpuBindGroup(BindGroup* handle) {
        this.handle = handle;
    }
    
    public static BindGroupEntry From<T>(int binding, GpuBuffer<T> buffer) where T : unmanaged
    {
        return new BindGroupEntry {
            Binding = (uint)binding,
            Buffer  = buffer.handle,
            Offset  = 0,
            Size    = (uint)(Unsafe.SizeOf<T>() * buffer.Length)
        };
    }
}