// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable CheckNamespace

using System;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

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
        return new RenderFrame(null, ((CommandRecorder)context).currentEncoder.handle);
    }
}

public readonly unsafe struct RenderFrame : IDisposable
{
    private  readonly   TextureView*    view;
    private  readonly   CommandEncoder* encoder;
    
    internal RenderFrame(TextureView* view, CommandEncoder* encoder) {
        this.view       = view;
        this.encoder    = encoder;
    }

    public RenderPass<T> BeginRenderPass<T>(RenderPassColorAttachment attachment) where T : struct
    {
        attachment.view = view;
        var renderPassDesc = new RenderPassDescriptor {
            colorAttachmentCount    = 1,
            colorAttachments        = &attachment
        };
        var passEncoder = wgpuCommandEncoderBeginRenderPass(encoder, &renderPassDesc); 
        wgpuRenderPassEncoderSetPipeline(passEncoder, null);
        wgpuRenderPassEncoderSetBindGroup(passEncoder, 0, null, 0, null);
        
        return new RenderPass<T>(passEncoder);
    }
    
    public void Dispose() { }
}

public readonly unsafe struct RenderPass<T> : IDisposable where T : struct
{
    internal readonly RenderPassEncoder* handle;
    
    internal RenderPass(RenderPassEncoder* handle) {
        this.handle = handle;
    }
    
    public void Dispose() { }
} 



