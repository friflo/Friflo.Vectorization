// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable CheckNamespace

using System;
using System.ComponentModel;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable UnusedTypeParameter
// ReSharper disable InconsistentNaming
namespace Friflo.Vectorization.WebGPU;

[AttributeUsage(AttributeTargets.Method)]
public sealed class ShaderAttribute<T> : Attribute where T : struct
{
    public ShaderAttribute (string wgsl) { }
}

public static class WgpuExtensions
{
    public static unsafe RenderFrame BeginFrame(this PipelineContext context)
    {
        var recorder = (CommandRecorder)context;
        return new RenderFrame(null, recorder);
    }
}

public readonly unsafe struct RenderFrame : IDisposable
{
    private  readonly   TextureView*    view;
    private  readonly   CommandRecorder recorder;
    
    internal RenderFrame(TextureView* view, CommandRecorder recorder) {
        this.view       = view;
        this.recorder   = recorder;
    }

    public RenderPass<T> BeginRenderPass<T>(RenderPassColorAttachment attachment) where T : struct
    {
        attachment.view = view;
        var renderPassDesc = new RenderPassDescriptor {
            colorAttachmentCount    = 1,
            colorAttachments        = &attachment
        };
        var passEncoder = wgpuCommandEncoderBeginRenderPass(recorder.currentEncoder.handle, &renderPassDesc);
        return new RenderPass<T>(passEncoder, recorder);
    }
    
    public void Dispose() { }
}

public readonly unsafe struct RenderPass<T> : IDisposable where T : struct
{
    public   readonly CommandRecorder       Recorder;
    internal readonly RenderPassEncoder*    handle;
    
    internal RenderPass(RenderPassEncoder* handle, CommandRecorder recorder) {
        this.handle = handle;
        Recorder    = recorder;
    }
    
    public RenderPass Value => new RenderPass(handle, Recorder);
    
    public void Dispose() { }
}

public readonly unsafe struct RenderPass
{
    public   readonly CommandRecorder       recorder;
    internal readonly RenderPassEncoder*    handle;
    
    internal RenderPass(RenderPassEncoder* handle, CommandRecorder recorder) {
        this.handle     = handle;
        this.recorder   = recorder;
    }

    public void SetPipeline(WgpuRenderPipeline renderPipeline)
    {
        wgpuRenderPassEncoderSetPipeline(handle, renderPipeline.handle);
    }

    public void SetBindGroup(uint groupIndex, WgpuBindGroup bindGroup, ulong hash)
    {
        wgpuRenderPassEncoderSetBindGroup(handle, groupIndex, bindGroup.handle, 0, null);
    }
    
    public void SetUniformBindGroup<T>(uint groupIndex, ref WgpuEffect effect, T uniform, ReadOnlySpan<byte> groupLabel) where T : unmanaged
    {
        uint alignedSize    = ((uint)sizeof(T) + (CommandRecorder.UniformAlignment - 1)) & ~(CommandRecorder.UniformAlignment - 1);
        var rec             = recorder;
        var bindGroup       = rec.GetUniformBindGroup(ref effect, alignedSize, groupLabel);

        uint offset = rec.uniformOffset;
        
        fixed (byte* pStaging = rec.stagingBuffer) {
            *(T*)(pStaging + offset) = uniform;
        }
        wgpuRenderPassEncoderSetBindGroup(handle, groupIndex, bindGroup.handle, 1, &offset);
        
        rec.uniformOffset = offset + alignedSize;
    }

    public void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        wgpuRenderPassEncoderDraw(handle, vertexCount, instanceCount, firstVertex, firstInstance);
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



