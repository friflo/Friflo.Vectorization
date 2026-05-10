// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Silk.NET.WebGPU;

// file contains structs created by:  GpuTask

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct WgpuEncoder
{
    private  readonly   WgpuTask        task;
    internal readonly   CommandEncoder* handle;
    public   override   string          ToString() => handle != null ? "Created" : "null";
    
    internal WgpuEncoder(WgpuTask task, CommandEncoder* handle) {
        this.task   = task;
        this.handle = handle;
    }
    
    // --- ComputePass methods
    public WgpuComputePass BeginComputePass(ReadOnlySpan<byte> passLabel)
    {
        fixed (byte* labelPtr = passLabel)
        {
            var desc            = new ComputePassDescriptor { Label = labelPtr };
            var passHandle      = task.wgpu.CommandEncoderBeginComputePass(handle, &desc);
            task.currentPass    = passHandle;
            return new WgpuComputePass(task, passHandle);
        }
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct WgpuComputePass : IDisposable {
    private readonly    WgpuTask            task;
    private readonly    ComputePassEncoder* handle;
    public  override    string              ToString() => handle != null ? "Created" : "null";
    
    public WgpuComputePass(WgpuTask task, ComputePassEncoder* handle) {
        this.task   = task;
        this.handle = handle;
    }
    
    public void Dispose() {
        End();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPipeline(WgpuComputePipeline pipeline) {
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
    public void SetBindGroup(int groupIndex, WgpuBindGroup bindGroup)
    {
        // 4th and 5th parameter are for dynamic offsets (0/null)
        task.wgpu.ComputePassEncoderSetBindGroup(handle, (uint)groupIndex, bindGroup.handle, 0, null);
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct WgpuBindGroup
{
    internal readonly   BindGroup*  handle;
    public              bool        IsCreated => handle != null;
    
    public   override   string      ToString() => handle != null ? "Created" : "null";
    
    internal WgpuBindGroup(BindGroup* handle) {
        this.handle = handle;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BindGroupEntry From<T>(int binding, in GPU.Buffer<T> buffer) where T : unmanaged
    {
        return new BindGroupEntry {
            Binding = (uint)binding,
            Buffer  = ((WgpuBuffer<T>)buffer.gpuBuffer._native).handle,
            Offset  = 0,
            Size    = (uint)(Unsafe.SizeOf<T>() * buffer.Count)
        };
    }
}