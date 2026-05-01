// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Silk.NET.WebGPU;

// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.Vectorization.GPU;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed unsafe class GpuTask : IDisposable
{
    internal readonly   GpuContext          context;
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
    
    // A simple state flag for the scheduler
    public              bool                IsSubmitted     { get; internal set; }
    public              bool                IsCompleted     { get; internal set; }
    

    internal GpuTask(GpuContext context, int taskIndex) {
        this.context    = context;
        this.taskIndex  = taskIndex;
        uniformBase     = (uint)(taskIndex * context.slotSize);
        stagingBuffer   = new byte[context.slotSize];
    }
    
    // The task provides / owns the Encoder
    public GpuEncoder GetEncoder(GpuContext ctx) {
        var encoder     = ctx.CreateEncoder(this); 
        currentEncoder  = encoder.handle;
        return encoder;
    }
    
    public GpuBindEntry AsUniformEntry<T>(int binding, T value) where T : unmanaged
    {
        var  ctx            = context;
        uint size           = (uint)sizeof(T);
        uint alignedOffset  = (uniformOffset + 255) & ~255u; // WebGPU requires Uniform offset must by 256 byte aligned
        
        if (alignedOffset + size > ctx.slotSize) {
            throw new IndexOutOfRangeException($"Uniform slot overflow. taskIndex: {taskIndex} slotSize: {ctx.slotSize}.");
        }
        // write directly to stagingBuffer
        fixed (byte* pDest = &stagingBuffer[alignedOffset]) {
            *(T*)pDest = value;
        }
        uint absoluteOffset = uniformBase + alignedOffset;
        
        uniformOffset = alignedOffset + size;
        return new GpuBindEntry(binding, ctx.globalUniformPool, absoluteOffset, size);
    }
    
    public void Finish(GpuEncoder encoder)
    {
        if (uniformOffset > 0) {
            fixed (byte* pData = stagingBuffer) {
                context.WriteBuffer(context.globalUniformPool, uniformBase, pData, uniformOffset);
            }
        }
        // TODO  Ultimate performance upgrade
        // If batch upload gets a bottleneck globalUniformPool must be created as "Persistent Mapped Buffer" (Host Visible).
        // This eliminates the WriteBuffer() call entirely because AsUniformEntry<> will than write directly in GPU memory.
        // This requires WGPU Buffer Map/Unmap Lifecycle Management
        
        var descriptor = new CommandBufferDescriptor();
        commandBuffer  = context.wgpu.CommandEncoderFinish(encoder.handle, &descriptor);

        if (currentEncoder != null) {
            context.wgpu.CommandEncoderRelease(currentEncoder);
            currentEncoder = null;
        }
    }
    
    public GpuBindGroup CreateBindGroup(GpuBindGroupLayout layout, Span<GpuBindEntry> bindEntries)
    {
        var nativeEntries = stackalloc BindGroupEntry[bindEntries.Length];

        for (int i = 0; i < bindEntries.Length; i++)
        {
            var bindEntry = bindEntries[i];
            nativeEntries[i] = new BindGroupEntry {
                Binding =   bindEntry.binding,
                Buffer =    bindEntry.bufferHandle,    // Direct handle to the native WGPUBuffer
                Offset =    bindEntry.offset,          // The byte offset (crucial for our Uniform Pool)
                Size =      bindEntry.size             // The byte size of the slice
            };
        }
        var descriptor = new BindGroupDescriptor {
            Layout      = layout.handle,
            EntryCount  = (uint)bindEntries.Length,
            Entries     = nativeEntries
        };
        var handle = context.wgpu.DeviceCreateBindGroup(context.DevicePtr, &descriptor);
        createdBindGroups.Add((nint)handle);
        return new GpuBindGroup(handle);
    }
    
    internal void Reset()
    {
        foreach (var ptr in createdBindGroups) {
            context.wgpu.BindGroupRelease((BindGroup*)ptr);
        }
        createdBindGroups.Clear();
        
        var bufferHandler = commandBuffer;
        if (bufferHandler != null) {
            context.wgpu.CommandBufferRelease(bufferHandler);
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
            context.wgpu.CommandEncoderRelease(currentEncoder);
            currentEncoder = null;
        }
    }
    
    internal void ClosePass() {
        if (currentPass != null) {
            context.wgpu.ComputePassEncoderEnd(currentPass);
            context.wgpu.ComputePassEncoderRelease(currentPass);
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
    public GpuComputePass BeginComputePass()
    {
        var desc            = new ComputePassDescriptor { Label = null };
        var passHandle      = task.context.wgpu.CommandEncoderBeginComputePass(handle, &desc);
        task.currentPass    = passHandle;
        return new GpuComputePass(task, passHandle);
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
        task.context.wgpu.ComputePassEncoderSetPipeline(handle, pipeline.handle);
    }
    
    public void DispatchWorkgroups(int workgroupCountX, int workgroupCountY, int workgroupCountZ) {
        task.context.wgpu.ComputePassEncoderDispatchWorkgroups(
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
        task.context.wgpu.ComputePassEncoderSetBindGroup(handle, (uint)groupIndex, bindGroup.handle, 0, null);
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct GpuBindGroup
{
    internal readonly BindGroup* handle;
    
    internal GpuBindGroup(BindGroup* handle) {
        this.handle = handle;
    }
}