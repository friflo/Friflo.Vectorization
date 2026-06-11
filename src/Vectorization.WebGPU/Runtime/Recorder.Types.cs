// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// file contains structs created by:  CommandRecorder

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
internal readonly unsafe struct WgpuEncoder
{
    internal readonly   CommandEncoder* handle;
    
    public   override   string          ToString() => handle != null ? "Created" : "null";
    
    internal WgpuEncoder(CommandEncoder* handle) {
        this.handle = handle;
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
internal readonly unsafe struct WgpuCommandBuffer
{
    internal readonly   CommandBuffer*  handle;
    
    public   override   string          ToString() => handle != null ? "Created" : "null";
    
    internal WgpuCommandBuffer(CommandBuffer* handle) {
        this.handle = handle;
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe ref struct WgpuComputePass : IDisposable
{
    private readonly    CommandRecorder     recorder;
    private readonly    ComputePassEncoder* handle;
    
    public  override    string              ToString() => handle != null ? "Created" : "null";
    
    internal WgpuComputePass(CommandRecorder recorder, ComputePassEncoder* handle) {
        this.recorder   = recorder;
        this.handle     = handle;
    }
    
    public void Dispose() {
        if (recorder.enablePassBatching == PassBatching.HazardDriven) {
            return;
        }
        recorder.FinishPass();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPipeline(WgpuComputePipeline pipeline)
    {
        if (!recorder.createNewPass && recorder.lastPipelineHandle == pipeline.handle) {
            return;
        }
        wgpuComputePassEncoderSetPipeline(handle, pipeline.handle);
        recorder.lastPipelineHandle = pipeline.handle;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DispatchWorkgroups(int workgroupCountX, int workgroupCountY, int workgroupCountZ)
    {
        wgpuComputePassEncoderDispatchWorkgroups(
            handle, 
            (uint)workgroupCountX, 
            (uint)workgroupCountY, 
            (uint)workgroupCountZ
        );
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBindGroup0(WgpuBindGroup bindGroup, ulong hash)
    {
        if (hash == recorder.lastBindGroup0_hash) {
            return;
        }
        wgpuComputePassEncoderSetBindGroup(handle, 0, bindGroup.handle, 0, null);
        recorder.lastBindGroup0_hash = hash;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBindGroup1(WgpuBindGroup bindGroup)
    {
        return;  // TODO UNI_REMOVE
        wgpuComputePassEncoderSetBindGroup(handle, 1, bindGroup.handle, 0, null);
    }
    
    public void SetUniform<T>(ref WgpuEffect effect, T uniform, ReadOnlySpan<byte> groupLabel) where T : unmanaged
    {
        uint alignedSize    = ((uint)sizeof(T) + (CommandRecorder.UniformAlignment - 1)) & ~(CommandRecorder.UniformAlignment - 1);
        var bindGroups      = recorder.uniformBindGroups;
        WgpuBindGroup bindGroup;
        
        if (effect.kernelId < bindGroups.Length) {
            bindGroup = bindGroups[effect.kernelId];
            if (bindGroup.handle == null) {
                bindGroup = recorder.CreateUniformBindGroup(ref effect, alignedSize, groupLabel);
            }
        } else {
            bindGroup = recorder.CreateUniformBindGroup(ref effect, alignedSize, groupLabel);
        }
        uint offset = recorder.uniformOffset;
        
        fixed (byte* pStaging = recorder.stagingBuffer) {
            *(T*)(pStaging + offset) = uniform;
        }
        wgpuComputePassEncoderSetBindGroup(handle, 1, bindGroup.handle, 1, &offset);
        
        recorder.uniformOffset = offset + alignedSize;
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
    public static BindGroupEntry From<T>(int binding, GpuBuffer<T> buffer) where T : unmanaged
    {
        return new BindGroupEntry {
            binding = (uint)binding,
            buffer  = (Buffer*)buffer.NativeHandle,
            offset  = 0,
            size    = (uint)(Unsafe.SizeOf<T>() * buffer.Length)
        };
    }
}
