// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Silk.NET.WebGPU;

namespace Friflo.Vectorization.GPU;

public sealed unsafe class GpuTask : IDisposable
{
    private readonly    GpuContext          context;
    private             CommandEncoder*     currentEncoder;
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
        var encoder = ctx.CreateEncoder(); 
        currentEncoder = encoder.handle;
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
        currentEncoder 	= null;
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
        if (currentEncoder != null) {
            context._wgpu.CommandEncoderRelease(currentEncoder);
            currentEncoder = null;
        }
    }
}

public readonly unsafe struct GpuEncoder
{
    internal readonly   GpuContext      context;
    internal readonly   CommandEncoder* handle;
    
    internal GpuEncoder(GpuContext context, CommandEncoder* handle) {
        this.context = context;
        this.handle  = handle;
    }
    
    // --- ComputePass methods
    public GpuComputePass BeginComputePass()
    {
        ComputePassDescriptor desc = new ComputePassDescriptor { Label = null };
        var passHandle = context._wgpu.CommandEncoderBeginComputePass(handle, &desc);
        
        return new GpuComputePass(this, passHandle);
    }
}