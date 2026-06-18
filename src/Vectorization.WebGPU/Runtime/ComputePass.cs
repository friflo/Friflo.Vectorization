// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;

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
    public void SetBindGroup(uint groupIndex, WgpuBindGroup bindGroup, ulong hash)
    {
        if (hash == recorder.lastBindGroup0_hash) {
            return;
        }
        wgpuComputePassEncoderSetBindGroup(handle, groupIndex, bindGroup.handle, 0, null);
        recorder.lastBindGroup0_hash = hash;
    }

    public void SetUniformBindGroup<T>(uint groupIndex, ref WgpuComputeEffect effect, T uniform, ReadOnlySpan<byte> groupLabel) where T : unmanaged
    {
        uint alignedSize    = ((uint)sizeof(T) + (CommandRecorder.UniformAlignment - 1)) & ~(CommandRecorder.UniformAlignment - 1);
        var rec             = recorder;
        var bindGroup       = rec.GetUniformBindGroup(effect.uniformLayout, alignedSize, ref rec.computeUniformGroups, groupLabel);

        uint offset = rec.uniformOffset;
        
        fixed (byte* pStaging = rec.stagingBuffer) {
            *(T*)(pStaging + offset) = uniform;
        }
        wgpuComputePassEncoderSetBindGroup(handle, groupIndex, bindGroup.handle, 1, &offset);
        
        rec.uniformOffset = offset + alignedSize;
    }
}

