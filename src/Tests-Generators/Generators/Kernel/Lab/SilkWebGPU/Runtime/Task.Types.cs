// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using Silk.NET.WebGPU;

// file contains structs created by:  SilkTask

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Kernel.SilkWebGPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct SilkEncoder
{
    private  readonly   SilkTask        task;
    internal readonly   CommandEncoder* handle;
    public   override   string          ToString() => handle != null ? "Created" : "null";
    
    internal SilkEncoder(SilkTask task, CommandEncoder* handle) {
        this.task   = task;
        this.handle = handle;
    }
    
    // --- ComputePass methods
    public SilkComputePass BeginComputePass(ReadOnlySpan<byte> passLabel)
    {
        fixed (byte* labelPtr = passLabel)
        {
            var desc            = new ComputePassDescriptor { Label = labelPtr };
            var passHandle      = task.wgpu.CommandEncoderBeginComputePass(handle, &desc);
            task.currentPass    = passHandle;
            return new SilkComputePass(task, passHandle);
        }
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct SilkComputePass : IDisposable {
    private readonly    SilkTask            task;
    private readonly    ComputePassEncoder* handle;
    public  override    string              ToString() => handle != null ? "Created" : "null";
    
    public SilkComputePass(SilkTask task, ComputePassEncoder* handle) {
        this.task   = task;
        this.handle = handle;
    }
    
    public void Dispose() {
        End();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPipeline(SilkComputePipeline pipeline) {
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
    public void SetBindGroup(int groupIndex, SilkBindGroup bindGroup)
    {
        // 4th and 5th parameter are for dynamic offsets (0/null)
        task.wgpu.ComputePassEncoderSetBindGroup(handle, (uint)groupIndex, bindGroup.handle, 0, null);
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct SilkBindGroup
{
    internal readonly   BindGroup*  handle;
    public              bool        IsCreated => handle != null;
    
    public   override   string      ToString() => handle != null ? "Created" : "null";
    
    internal SilkBindGroup(BindGroup* handle) {
        this.handle = handle;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BindGroupEntry From<T>(int binding, in GpuBuffer<T> buffer) where T : unmanaged
    {
        return new BindGroupEntry {
            Binding = (uint)binding,
            Buffer  = ((SilkBuffer<T>)buffer).handle,
            Offset  = 0,
            Size    = (uint)(Unsafe.SizeOf<T>() * buffer.Length)
        };
    }
}