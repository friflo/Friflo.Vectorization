// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// file contains structs created by:  CommandRecorder

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
[InlineArray(4)] // generator creates only 2 bind groups
internal struct BindGroups
{
    private nint _element0;
}

[EditorBrowsable(EditorBrowsableState.Never)]
internal readonly unsafe struct WgpuEncoder
{
    internal readonly   CommandEncoder*     handle;
    
    public   override   string              ToString() => handle != null ? "Created" : "null";
    
    internal WgpuEncoder(CommandEncoder* handle) {
        this.handle = handle;
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe ref struct WgpuComputePass : IDisposable
{
    private readonly    CommandRecorder     recorder;
    private readonly    ComputePassEncoder* handle;
    private readonly    ReadOnlySpan<byte>  label;
    
    public  override    string              ToString() => handle != null ? "Created" : "null";
    
    internal WgpuComputePass(CommandRecorder recorder, ComputePassEncoder* handle, ReadOnlySpan<byte> label) {
        this.recorder   = recorder;
        this.handle     = handle;
        this.label      = label;
    }
    
    public void Dispose() {
        End();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPipeline(WgpuComputePipeline pipeline) {
        wgpuComputePassEncoderSetPipeline(handle, pipeline.handle);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DispatchWorkgroups(int workgroupCountX, int workgroupCountY, int workgroupCountZ) {
        wgpuComputePassEncoderDispatchWorkgroups(
            handle, 
            (uint)workgroupCountX, 
            (uint)workgroupCountY, 
            (uint)workgroupCountZ
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void End() {
        for (int n = 0; n < recorder.createdBindGroupsCount; n++) {
            wgpuBindGroupRelease((BindGroup*)recorder.createdBindGroups[n]);
        }
        recorder.createdBindGroupsCount = 0;
        recorder.ClosePass();
        
        recorder.Finish(label);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBindGroup(int groupIndex, WgpuBindGroup bindGroup)
    {
        // 4th and 5th parameter are for dynamic offsets (0/null)
        wgpuComputePassEncoderSetBindGroup(handle, (uint)groupIndex, bindGroup.handle, 0, null);
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
    public static BindGroupEntry From<T>(int binding, in GpuBuffer<T> buffer) where T : unmanaged
    {
        return new BindGroupEntry {
            binding = (uint)binding,
            buffer  = (Buffer*)buffer.NativeHandle,
            offset  = 0,
            size    = (uint)(Unsafe.SizeOf<T>() * buffer.Length)
        };
    }
}
