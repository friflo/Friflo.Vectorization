// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;

public readonly unsafe ref struct RenderPassInternal
{
    public   readonly   CommandRecorder       Recorder;
    private  readonly   RenderPassEncoder*    handle;
    
    internal RenderPassInternal(RenderPassEncoder* handle, CommandRecorder recorder) {
        this.handle = handle;
        Recorder    = recorder;
    }

    public void SetPipeline(WgpuRenderPipeline renderPipeline)
    {
        wgpuRenderPassEncoderSetPipeline(handle, renderPipeline.handle);
    }

    /// <summary>Used without preceding <see cref="AddUniform"/> call. </summary>
    public void SetBindGroup(uint groupIndex, WgpuBindGroup bindGroup)
    {
        wgpuRenderPassEncoderSetBindGroup(handle, groupIndex, bindGroup.handle, 0, null);
    }

    /// <summary>Used with preceding <see cref="AddUniform"/> calls. </summary>
    public void SetBindGroupUniforms(uint groupIndex, WgpuBindGroup bindGroup)
    {
        var rec     = Recorder;
        var count   = rec.uniformOffsetsCount;
        rec.uniformOffsetsCount = 0;
        fixed(uint* offsets = rec.uniformOffsets) {
            wgpuRenderPassEncoderSetBindGroup(handle, groupIndex, bindGroup.handle, count, offsets);
        }
    }
    
    public void AddUniform<T>(in T uniform) where T : unmanaged
    {
        uint alignedSize    = ((uint)sizeof(T) + (CommandRecorder.UniformAlignment - 1)) & ~(CommandRecorder.UniformAlignment - 1);
        var rec             = Recorder;
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
    
    /// <summary> Set bind group with a uniform for a group layout with only a single layout single entry. </summary>
    public void SetBindGroupUniform<T>(uint groupIndex, ref WgpuBindGroup bindGroup, in T uniform, in PipelineCache pipelineCache, ReadOnlySpan<byte> groupLabel) where T : unmanaged
    {
        var rec = Recorder;
        if (!bindGroup.IsCreated) {
            var entry   = rec.CreateUniformBindGroupEntry<T>(0);
            bindGroup   = rec.CreateBindGroupInternal(pipelineCache.layouts[(int)groupIndex], entry, groupLabel);
        }
        uint alignedSize    = ((uint)sizeof(T) + (CommandRecorder.UniformAlignment - 1)) & ~(CommandRecorder.UniformAlignment - 1);
        uint offset         = rec.uniformOffset;
        rec.uniformOffset   = offset + alignedSize;
        
        ref byte dst = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(rec.stagingBuffer), (nint)offset);
        Unsafe.As<byte, T>(ref dst) = uniform;
        /* fixed version
         fixed (byte* pStaging = rec.stagingBuffer) {
            *(T*)(pStaging + offset) = uniform;
        } */
        wgpuRenderPassEncoderSetBindGroup(handle, groupIndex, bindGroup.handle, 1, &offset);
    }

    /// <summary>
    /// See <see cref="VertexBufferAttribute"/> documentation for setting <c>arrayStride</c> in a <see cref="GpuVertexBufferLayout"/>.  
    /// </summary>
    public void SetVertexBuffer<T>(in InBuffer<T> buffer, int slot) where T : unmanaged
    {
        ulong offset = (ulong)(buffer.Offset * sizeof(T)); // size in bytes
        ulong size   = (ulong)(buffer.Length * sizeof(T)); // size in bytes
        
        wgpuRenderPassEncoderSetVertexBuffer(handle, (uint)slot, (Buffer*)buffer.Buffer.NativeHandle, offset, size);
    }
    
    public void Draw<T>(in InBuffer<T> buffer, int slot, RenderConfig config, int instanceCount, int firstVertex, int firstInstance) where T : unmanaged
    {
        int size        = buffer.Length * sizeof(T); // size in bytes
        int vertexCount = size / config.Descriptor.VertexState.buffers[slot].arrayStride; // arrayStride == 0 should result in DivideByZeroException 
        wgpuRenderPassEncoderDraw(handle, (uint)vertexCount, (uint)instanceCount, (uint)firstVertex, (uint)firstInstance);
    }

    public void Draw(int vertexCount, int instanceCount, int firstVertex, int firstInstance)
    {
        wgpuRenderPassEncoderDraw(handle, (uint)vertexCount, (uint)instanceCount, (uint)firstVertex, (uint)firstInstance);
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct WgpuRenderPipeline
{
    internal readonly   RenderPipeline* handle;
    public   override   string          ToString() => handle != null ? "Created" : "null";
    
    internal WgpuRenderPipeline(RenderPipeline* handle) {
        this.handle = handle;
    }
}
