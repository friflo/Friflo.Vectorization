// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    
    // ------------ aligned methods: RenderPassInternal, WgpuComputePass ------------
    
    internal WgpuComputePass(CommandRecorder recorder, ComputePassEncoder* handle) {
        this.recorder   = recorder;
        this.handle     = handle;
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
    
    /// <summary>Set bind group without a uniform. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBindGroup(uint groupIndex, WgpuBindGroup bindGroup)
    {
        if (bindGroup.handle == recorder.lastBufferBindGroup) {
            return;
        }
        wgpuComputePassEncoderSetBindGroup(handle, groupIndex, bindGroup.handle, 0, null);
        recorder.lastBufferBindGroup = bindGroup.handle;
    }
    
    /// <summary> A sequence of these calls are finished with <see cref="SetBindGroupUniforms"/>. </summary>
    public void AddUniform<T>(in T uniform) where T : unmanaged
    {
        uint alignedSize    = ((uint)sizeof(T) + (CommandRecorder.UniformAlignment - 1)) & ~(CommandRecorder.UniformAlignment - 1);
        var rec             = recorder;
        uint offset         = rec.uniformOffset;
        rec.uniformOffset   = offset + alignedSize;
        rec.uniformOffsets[rec.uniformOffsetsCount++] = offset;
        
        ref byte dst = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(rec.stagingBuffer), offset);
        Unsafe.As<byte, T>(ref dst) = uniform;
        /* fixed version
        fixed (byte* pStaging = rec.stagingBuffer) {
            *(T*)(pStaging + offset) = uniform;
        } */
    }
    
    /// <summary>Used with preceding <see cref="AddUniform"/> calls. </summary>
    public void SetBindGroupUniforms(uint groupIndex, WgpuBindGroup bindGroup)
    {
        var rec     = recorder;
        var count   = rec.uniformOffsetsCount;
        rec.uniformOffsetsCount = 0;
        fixed(uint* offsets = rec.uniformOffsets) {
            wgpuComputePassEncoderSetBindGroup(handle, groupIndex, bindGroup.handle, count, offsets);
        }
    }
    
    /// <summary> Set bind group with a single uniform. Create / cache bind group. </summary>
    public void SetBindGroupUniform<T>(uint groupIndex, int binding, ref WgpuBindGroup bindGroup, in T uniform, in ComputeCache pipelineCache, ReadOnlySpan<byte> groupLabel) where T : unmanaged
    {
        var rec = recorder;
        if (!bindGroup.IsCreated) {
            var entry   = rec.CreateUniformBindGroupEntry<T>(binding); // always binding: 0  for [Kernel] methods
            bindGroup   = rec.CreateBindGroupInternal(pipelineCache.layouts[(int)groupIndex], entry, groupLabel);
        }
        uint alignedSize    = ((uint)sizeof(T) + (CommandRecorder.UniformAlignment - 1)) & ~(CommandRecorder.UniformAlignment - 1);
        uint offset         = rec.uniformOffset;
        rec.uniformOffset   = offset + alignedSize;
        
        ref byte dst = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(rec.stagingBuffer), offset);
        Unsafe.As<byte, T>(ref dst) = uniform;
        /* fixed version
        fixed (byte* pStaging = rec.stagingBuffer) {
            *(T*)(pStaging + offset) = uniform;
        } */
        wgpuComputePassEncoderSetBindGroup(handle, groupIndex, bindGroup.handle, 1, &offset);
    }
    


    // -------------------- pass specific methods --------------------
    
    public void Dispose() {
        if (recorder.enablePassBatching == PassBatching.HazardDriven) {
            return;
        }
        recorder.FinishPass();
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
}

