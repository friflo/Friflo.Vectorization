// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Silk.NET.WebGPU;

// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.Vectorization.GPU;

public sealed unsafe class GpuTask : IDisposable
{
    internal readonly   GpuContext          context;
    private             CommandEncoder*     currentEncoder;             // GpuTask owns CommandEncoder* and ensures release
    internal            ComputePassEncoder* currentPass;                // GpuTask owns ComputePassEncoder* and ensures release
    private readonly    List<nint>          createdBindGroups = new();  // GpuTask owns all created BindGroup* and ensures release  
    internal            GpuCommandBuffer    CommandBuffer   { get; }
    private readonly    List<GpuTask>       dependencies = new();  // Tasks that MUST finish before this one starts
    
    // A simple state flag for the scheduler
    public              bool                IsSubmitted     { get; internal set; }
    public              bool                IsCompleted     { get; internal set; }
    

    internal GpuTask(GpuContext context, int taskIndex) {
        this.context    = context;
        CommandBuffer   = new GpuCommandBuffer(context);
    }
    
    // The task provides / owns the Encoder
    public GpuEncoder GetEncoder(GpuContext ctx) {
        var encoder     = ctx.CreateEncoder(this); 
        currentEncoder  = encoder.handle;
        return encoder;
    }
    
    public void Finish(GpuEncoder encoder)
    {
        var descriptor = new CommandBufferDescriptor();
        CommandBuffer.handle = context.wgpu.CommandEncoderFinish(encoder.handle, &descriptor);

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
        var handle = layout.context.wgpu.DeviceCreateBindGroup(context.DevicePtr, &descriptor);
        createdBindGroups.Add((nint)handle);
        return new GpuBindGroup(handle);
    }

    
    internal void Reset()
    {
        foreach (var ptr in createdBindGroups) {
            context.wgpu.BindGroupRelease((BindGroup*)ptr);
        }
        createdBindGroups.Clear();
        
        var bufferHandler = CommandBuffer.handle;
        if (bufferHandler != null) {
            CommandBuffer.context.wgpu.CommandBufferRelease(bufferHandler);
            CommandBuffer.handle = null;
        }
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

public readonly unsafe struct GpuBindGroup
{
    internal readonly BindGroup* handle;
    
    internal GpuBindGroup(BindGroup* handle) {
        this.handle = handle;
    }
}