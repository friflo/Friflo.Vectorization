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
    private             CommandEncoder*     currentEncoder; // GpuTask owns the pointer and ensures release
    internal            ComputePassEncoder* currentPass;    // GpuTask owns the pointer and ensures release
    internal            GpuCommandBuffer    CommandBuffer { get; }
    private readonly    List<GpuTask>       dependencies = new();  // Tasks that MUST finish before this one starts
    
    // A simple state flag for the scheduler
    public              bool                IsSubmitted { get; internal set; }
    public              bool                IsCompleted { get; internal set; }
    

    // Constructor for real GPU work
    internal GpuTask(GpuContext context) {
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
        CommandBuffer.Handle = context._wgpu.CommandEncoderFinish(encoder.handle, &descriptor);

        if (currentEncoder != null) {
            context._wgpu.CommandEncoderRelease(currentEncoder);
            currentEncoder = null;
        }
    }
    
    internal void Reset()
    {
        var bufferHandler = CommandBuffer.Handle;
        if (bufferHandler != null) {
            CommandBuffer.Context._wgpu.CommandBufferRelease(bufferHandler);
            CommandBuffer.Handle = null;
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
            context._wgpu.CommandEncoderRelease(currentEncoder);
            currentEncoder = null;
        }
    }
    
    internal void ClosePass() {
        if (currentPass != null) {
            context._wgpu.ComputePassEncoderEnd(currentPass);
            context._wgpu.ComputePassEncoderRelease(currentPass);
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
        var passHandle      = task.context._wgpu.CommandEncoderBeginComputePass(handle, &desc);
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
        task.context._wgpu.ComputePassEncoderSetPipeline(handle, pipeline.Handle);
    }
    
    public void DispatchWorkgroups(int workgroupCountX, int workgroupCountY, int workgroupCountZ) {
        task.context._wgpu.ComputePassEncoderDispatchWorkgroups(
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
        task.context._wgpu.ComputePassEncoderSetBindGroup(handle, (uint)groupIndex, bindGroup.Handle, 0, null);
    }
}
