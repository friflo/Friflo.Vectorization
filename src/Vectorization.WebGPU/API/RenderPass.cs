// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.ComponentModel;
using System.Diagnostics;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


public readonly unsafe ref struct RenderPass : IDisposable
{
    private  readonly   CommandRecorder     Recorder;
    private  readonly   RenderPassEncoder*  handle;
    
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public RenderPassInternal Internal => new (handle, Recorder);

    
    internal RenderPass(RenderPassEncoder* handle, CommandRecorder recorder) {
        this.handle = handle;
        Recorder    = recorder;

    }
    
    /// <summary>
    /// See: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/setBlendConstant">Draw()</a> 
    /// </summary>
    public void SetBlendConstant(in GpuColor color)
    {
        var native = color.GetNative();
        wgpuRenderPassEncoderSetBlendConstant(handle, &native);
    }
    
    
    public void Dispose()
    {
        if (handle != null) {
            Recorder.Reset();
            wgpuRenderPassEncoderEnd(handle);
            wgpuRenderPassEncoderRelease(handle);
        }
    }
}

