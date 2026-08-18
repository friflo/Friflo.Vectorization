// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.ComponentModel;
using System.Diagnostics;
using Friflo.WGPU.Runtime;
using static Friflo.WGPU.Runtime.WebGPU_native;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.WGPU;


public readonly unsafe ref struct RenderPass : IDisposable
{
    private  readonly   CommandRecorder     Recorder;
    private  readonly   Size2D              windowSize;
    
    private             RenderPassEncoder*  Handle
    { get {
        var handle = Recorder.renderPassEncoder;
        return handle != null ? handle : throw new ObjectDisposedException(nameof(RenderPass));
    } }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public RenderPassInternal Internal => new (Recorder, Handle);

    
    internal RenderPass(CommandRecorder recorder, Size2D windowSize) {
        Recorder        = recorder;
        this.windowSize = windowSize;
    }

#region --- rasterization & blending states
    /// <summary>
    /// See: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/setBlendConstant">MDN: SetBlendConstant()</a> 
    /// </summary>
    public void SetBlendConstant(in GpuColor color)
    {
        var native = color.GetNative();
        wgpuRenderPassEncoderSetBlendConstant(Handle, &native);
    }
    
    /// <summary>
    /// See: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/setStencilReference">MDN: SetStencilReference()</a> 
    /// </summary>
    public void SetStencilReference(int reference)
    {
        wgpuRenderPassEncoderSetStencilReference(Handle, (uint)reference);
    }
#endregion




#region --- viewport & clipping
    /// <summary>
    /// See: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/setScissorRect">MDN: SetScissorRect()</a> 
    /// </summary>
    public readonly void SetScissorRect(int x, int y, int width, int height)
    {
        int x1 = Math.Clamp(x,          0, windowSize.width);
        int y1 = Math.Clamp(y,          0, windowSize.height);
        int x2 = Math.Clamp(x + width,  0, windowSize.width);
        int y2 = Math.Clamp(y + height, 0, windowSize.height);

        int clampedW = Math.Max(0, x2 - x1);
        int clampedH = Math.Max(0, y2 - y1);
        wgpuRenderPassEncoderSetScissorRect(Handle, (uint)x1, (uint)y1, (uint)clampedW, (uint)clampedH);
    }
    
    /// <summary>
    /// See: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/setViewport">MDN: SetViewport()</a> 
    /// </summary>
    public readonly void SetViewport(float x, float y, float width, float height, float minDepth, float maxDepth)
    {
        wgpuRenderPassEncoderSetViewport(Handle, x, y, width, height, minDepth, maxDepth);
    }
#endregion
    

    
#region --- occlusion query
    /// <summary>
    /// See: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/beginOcclusionQuery">MDN: BeginOcclusionQuery()</a> 
    /// </summary>
    public void BeginOcclusionQuery(int queryIndex)
    {
        wgpuRenderPassEncoderBeginOcclusionQuery(Handle, (uint)queryIndex);
    }

    /// <summary>
    /// See: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/endOcclusionQuery">MDN: EndOcclusionQuery()</a> 
    /// </summary>
    public void EndOcclusionQuery()
    {
        wgpuRenderPassEncoderEndOcclusionQuery(Handle);
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
        wgpuRenderPassEncoderPushDebugGroup(Handle, labelView);
    }
    
    /// <summary>
    /// See: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/popDebugGroup">MDN: PopDebugGroup()</a> 
    /// </summary>
    public void PopDebugGroup()
    {
        wgpuRenderPassEncoderPopDebugGroup(Handle);
    }
    
    /// <summary>
    /// See: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/insertDebugMarker">MDN: InsertDebugMarker()</a> 
    /// </summary>
    public void InsertDebugMarker(string markerLabel)
    {
        var labelMaxCount   = WgpuUtils.GetMaxCount(markerLabel);
        var labelBuffer     = stackalloc byte[labelMaxCount];
        var labelView       = WgpuUtils.CopyToStringView(markerLabel, labelBuffer, labelMaxCount);
        wgpuRenderPassEncoderInsertDebugMarker(Handle, labelView);
    }
#endregion
    
    
    public void Dispose()
    {
        var handle = Handle;
        if (handle == null) {
            return;
        }
        Recorder.renderPassEncoder = null;
        Recorder.Reset();
        wgpuRenderPassEncoderEnd(handle);
        wgpuRenderPassEncoderRelease(handle);
    }
}

