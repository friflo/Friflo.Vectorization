// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Silk.NET.WebGPU;

namespace Friflo.Vectorization.GPU;

public sealed unsafe class GpuTask : IDisposable
{
    private readonly    GpuContext          context;
    private             CommandEncoder*     currentEncoder; // GpuTask owns the pointer and ensures release
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

public unsafe class GpuComputePass : IDisposable {
    private readonly    GpuEncoder          _encoder;
    public              ComputePassEncoder* Handle { get; }
    private             bool                _hasEnded = false;
    
    public GpuComputePass(GpuEncoder encoder, ComputePassEncoder* handle)
    {
        _encoder = encoder;
        Handle   = handle;
    }
    
    public void Dispose() {
        End(); // Sicherstellen, dass der Pass beendet wurde
        // Den nativen Pass-Encoder freigeben
        if (Handle != null) _encoder.context._wgpu.ComputePassEncoderRelease(Handle);
    }

    public void SetPipeline(GpuComputePipeline pipeline)
    {
        _encoder.context._wgpu.ComputePassEncoderSetPipeline(Handle, pipeline.Handle);
    }
    
    public void DispatchWorkgroups(int workgroupCountX, int workgroupCountY, int workgroupCountZ) {
        _encoder.context._wgpu.ComputePassEncoderDispatchWorkgroups(
            Handle, 
            (uint)workgroupCountX, 
            (uint)workgroupCountY, 
            (uint)workgroupCountZ
        );
    }

    public void End()
    {
        if (!_hasEnded) {
            _encoder.context._wgpu.ComputePassEncoderEnd(Handle);
            _hasEnded = true;
        }
    }

    public void SetBindGroup(int groupIndex, GpuBindGroup bindGroup)
    {
        // Der vierte und fünfte Parameter sind für dynamische Offsets (hier 0/null)
        _encoder.context._wgpu.ComputePassEncoderSetBindGroup(Handle, (uint)groupIndex, bindGroup.Handle, 0, null);
    }
}
