// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Silk.NET.WebGPU;

// file contains structs created by:  GpuTask
namespace Friflo.Vectorization.GPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct GpuEncoder
{
    private  readonly   GpuTask         task;
    internal readonly   CommandEncoder* handle;
    public   override   string          ToString() => handle != null ? "Created" : "null";
    
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
    public   override   string              ToString() => handle != null ? "Created" : "null";
    
    public GpuComputePass(GpuTask task, ComputePassEncoder* handle) {
        this.task   = task;
        this.handle = handle;
    }
    
    public void Dispose() {
        End();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPipeline(GpuComputePipeline pipeline) {
        task.wgpu.ComputePassEncoderSetPipeline(handle, pipeline.handle);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DispatchWorkgroups(int workgroupCountX, int workgroupCountY, int workgroupCountZ) {
        task.wgpu.ComputePassEncoderDispatchWorkgroups(
            handle, 
            (uint)workgroupCountX, 
            (uint)workgroupCountY, 
            (uint)workgroupCountZ
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void End() {
        task.ClosePass(); 
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBindGroup(int groupIndex, GpuBindGroup bindGroup)
    {
        // 4th and 5th parameter are for dynamic offsets (0/null)
        task.wgpu.ComputePassEncoderSetBindGroup(handle, (uint)groupIndex, bindGroup.handle, 0, null);
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct GpuBindGroup
{
    internal readonly   BindGroup*  handle;
    public   override   string      ToString() => handle != null ? "Created" : "null";
    
    internal GpuBindGroup(BindGroup* handle) {
        this.handle = handle;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BindGroupEntry From<T>(int binding, in Buffer<T> buffer) where T : unmanaged
    {
        return new BindGroupEntry {
            Binding = (uint)binding,
            Buffer  = buffer.gpuBuffer.handle,
            Offset  = 0,
            Size    = (uint)(Unsafe.SizeOf<T>() * buffer.Count)
        };
    }
}