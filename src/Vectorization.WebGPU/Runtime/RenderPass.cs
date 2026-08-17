// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.GPU;
using static Friflo.WGPU.Runtime.WebGPU_native;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe ref struct RenderPassInternal
{
    public   readonly   CommandRecorder     Recorder;
    private  readonly   RenderPassEncoder*  handle;
    
    public  override    string              ToString() => handle != null ? "Created" : "null";
    
    // ------------ aligned methods: RenderPassInternal, WgpuComputePass ------------
    
    internal RenderPassInternal(CommandRecorder recorder, RenderPassEncoder* handle) {
        Recorder    = recorder;
        this.handle = handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPipeline(WgpuRenderPipeline pipeline)
    {
        wgpuRenderPassEncoderSetPipeline(handle, pipeline.handle);
    }

    /// <summary>Set bind group without a uniform. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBindGroup(uint groupIndex, WgpuBindGroup bindGroup)
    {
        wgpuRenderPassEncoderSetBindGroup(handle, groupIndex, bindGroup.handle, 0, null);
    }

    /// <summary> A sequence of these calls are finished with <see cref="SetBindGroupUniforms"/>. </summary>
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
    
    /// <summary> Set bind group with a single uniform. Create / cache bind group. </summary>
    public void SetBindGroupUniform<T>(uint groupIndex, int binding, ref WgpuBindGroup bindGroup, in T uniform, in PipelineCache pipelineCache, ReadOnlySpan<byte> groupLabel) where T : unmanaged
    {
        var rec = Recorder;
        if (!bindGroup.IsCreated) {
            var entry   = rec.CreateUniformBindGroupEntry<T>(binding);
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

    
    
    // -------------------- pass specific methods --------------------
    
    /// <summary>
    /// See <see cref="VertexBufferAttribute"/> documentation for setting <c>arrayStride</c> in a <see cref="GpuVertexBufferLayout"/>.  
    /// </summary>
    public void SetVertexBuffer<T>(in InBuffer<T> buffer, int slot) where T : unmanaged
    {
        ulong offset = (ulong)(buffer.Offset * sizeof(T)); // size in bytes
        ulong size   = (ulong)(buffer.Length * sizeof(T)); // size in bytes
        
        wgpuRenderPassEncoderSetVertexBuffer(handle, (uint)slot, (Buffer*)buffer.Buffer.NativeHandle, offset, size);
    }
    
    public void SetIndexBuffer<T>(in InBuffer<T> buffer, IndexFormat format) where T : unmanaged
    {
        ulong offset = (ulong)(buffer.Offset * sizeof(T)); // size in bytes
        ulong size   = (ulong)(buffer.Length * sizeof(T)); // size in bytes
        
        wgpuRenderPassEncoderSetIndexBuffer(handle, (Buffer*)buffer.Buffer.NativeHandle, format, offset, size);
    }
    
    /// <summary> Draws an <see cref="InBuffer{T}"/> annotated with <see cref="IndexBufferAttribute"/>. </summary>
    public void DrawIndexed<T>(in InBuffer<T> indexBuffer, DrawArgs args) where T : unmanaged
    {
        int indexCount = args.count > 0 ? args.count : indexBuffer.Length;
        wgpuRenderPassEncoderDrawIndexed(handle, (uint)indexCount, (uint)args.instanceCount, (uint)args.first, 0, (uint)args.firstInstance);
    }
    
    /// <summary>Draws an <see cref="InBuffer{T}"/> annotated with <see cref="VertexBufferAttribute"/>. </summary> 
    public void Draw<T>(in InBuffer<T> vertexBuffer, int slot, RenderConfig config, DrawArgs args) where T : unmanaged
    {
        int vertexCount = args.count > 0
            ? args.count
            : vertexBuffer.Length * sizeof(T) / config.Descriptor.VertexState.buffers[slot].arrayStride; // arrayStride == 0 should result in DivideByZeroException
        wgpuRenderPassEncoderDraw(handle, (uint)vertexCount, (uint)args.instanceCount, (uint)args.first, (uint)args.firstInstance);
    }

    /// <summary> Draw a buffer annotated with <see cref="storageAttribute"/> or <see cref="uniformAttribute"/>. </summary>
    public void Draw<T>(in InBuffer<T> buffer, DrawArgs args) where T : unmanaged
    {
        int vertexCount = args.count > 0 ? args.count : buffer.Length;
        wgpuRenderPassEncoderDraw(handle, (uint)vertexCount, (uint)args.instanceCount, (uint)args.first, (uint)args.firstInstance);
    }
    
    /// <summary> Used for methods using no [Draw] parameter.</summary>
    public void Draw(DrawArgs args)
    {
        wgpuRenderPassEncoderDraw(handle, (uint)args.count, (uint)args.instanceCount, (uint)args.first, (uint)args.firstInstance);
    }
    
    // not tested
    public void DrawIndirect<T>(in InBuffer<T> buffer, DrawIndirectArgs args) where T : unmanaged
    {
        int actualCount = args.drawCount <= 0 ? 1 : args.drawCount;
        var byteOffset  = (ulong)args.offset * (ulong)sizeof(T);
        if (actualCount > 1) {
            // requires WebGPU Multi-Draw extension
            wgpuRenderPassEncoderMultiDrawIndirect(handle, (Buffer*)buffer.Handle, byteOffset, (uint)actualCount);
        } else {
            wgpuRenderPassEncoderDrawIndirect(handle, (Buffer*)buffer.Handle, byteOffset);
        }
    }
    
    // not tested
    public void DrawIndexedIndirect<T>(in InBuffer<T> buffer, DrawIndirectArgs args) where T : unmanaged
    {
        int actualCount = args.drawCount <= 0 ? 1 : args.drawCount;
        var byteOffset  = (ulong)args.offset * (ulong)sizeof(T);
        if (actualCount > 1) {
            // requires WebGPU Multi-Draw extension
            wgpuRenderPassEncoderMultiDrawIndexedIndirect(handle, (Buffer*)buffer.Handle, byteOffset, (uint)actualCount);
        } else {
            wgpuRenderPassEncoderDrawIndexedIndirect(handle, (Buffer*)buffer.Handle, byteOffset);
        }
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
