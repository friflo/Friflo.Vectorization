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


public unsafe ref struct RenderPass : IDisposable
{
    private  readonly   CommandRecorder     Recorder;
    private             RenderPassEncoder*  handle;
    
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public RenderPassInternal Internal => new (handle, Recorder);

    
    internal RenderPass(RenderPassEncoder* handle, CommandRecorder recorder) {
        this.handle = handle;
        Recorder    = recorder;
    }

#region --- rasterization & blending states
    /// <summary>
    /// See: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/setBlendConstant">MDN: SetBlendConstant()</a> 
    /// </summary>
    public void SetBlendConstant(in GpuColor color)
    {
        var native = color.GetNative();
        wgpuRenderPassEncoderSetBlendConstant(handle, &native);
    }
    
    /// <summary>
    /// See: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/setStencilReference">MDN: SetStencilReference()</a> 
    /// </summary>
    public void SetStencilReference(int reference)
    {
        wgpuRenderPassEncoderSetStencilReference(handle, (uint)reference);
    }
#endregion




#region --- viewport & clipping
    /// <summary>
    /// See: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/setScissorRect">MDN: SetScissorRect()</a> 
    /// </summary>
    public void SetScissorRect(int x, int y, int width, int height)
    {
        wgpuRenderPassEncoderSetScissorRect(handle, (uint)x, (uint)y, (uint)width, (uint)height);
    }
    
    /// <summary>
    /// See: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/setViewport">MDN: SetViewport()</a> 
    /// </summary>
    public void SetViewport(float x, float y, float width, float height, float minDepth, float maxDepth)
    {
        wgpuRenderPassEncoderSetViewport(handle, x, y, width, height, minDepth, maxDepth);
    }
#endregion
    

    
#region --- occlusion query
    /// <summary>
    /// See: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/beginOcclusionQuery">MDN: BeginOcclusionQuery()</a> 
    /// </summary>
    public void BeginOcclusionQuery(int queryIndex)
    {
        wgpuRenderPassEncoderBeginOcclusionQuery(handle, (uint)queryIndex);
    }

    /// <summary>
    /// See: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/endOcclusionQuery">MDN: EndOcclusionQuery()</a> 
    /// </summary>
    public void EndOcclusionQuery()
    {
        wgpuRenderPassEncoderEndOcclusionQuery(handle);
    }
#endregion



#region --- debug group
    /// <summary>
    /// See: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/pushDebugGroup">MDN: PushDebugGroup()</a> 
    /// </summary>
    public void PushDebugGroup(string groupLabel)
    {
        var labelMaxCount   = WgpuUtils.GetMaxCount(groupLabel);
        var labelBuffer     = stackalloc byte[labelMaxCount];
        var labelView       = WgpuUtils.CopyToStringView(groupLabel, labelBuffer, labelMaxCount);
        wgpuRenderPassEncoderPushDebugGroup(handle, labelView);
    }
    
    /// <summary>
    /// See: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/popDebugGroup">MDN: PopDebugGroup()</a> 
    /// </summary>
    public void PopDebugGroup()
    {
        wgpuRenderPassEncoderPopDebugGroup(handle);
    }
    
    /// <summary>
    /// See: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/insertDebugMarker">MDN: InsertDebugMarker()</a> 
    /// </summary>
    public void InsertDebugMarker(string markerLabel)
    {
        var labelMaxCount   = WgpuUtils.GetMaxCount(markerLabel);
        var labelBuffer     = stackalloc byte[labelMaxCount];
        var labelView       = WgpuUtils.CopyToStringView(markerLabel, labelBuffer, labelMaxCount);
        wgpuRenderPassEncoderInsertDebugMarker(handle, labelView);
    }
#endregion
    
    
    public void Dispose()
    {
        if (handle == null) {
            return;
        }
        Recorder.Reset();
        wgpuRenderPassEncoderEnd(handle);
        wgpuRenderPassEncoderRelease(handle);
        handle = null;
    }
}

