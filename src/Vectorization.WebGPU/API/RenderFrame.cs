// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable TooWideLocalVariableScope
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


public static class WgpuExtensions
{
    public static unsafe RenderFrame BeginFrame(this PipelineContext context, WgpuSurface surface)
    {
        if (surface.handle == null) {
            throw new InvalidOperationException("WgpuSurface is null");
        }
        var recorder = (CommandRecorder)context;
        SurfaceTexture surfaceTexture;
        wgpuSurfaceGetCurrentTexture(surface.handle, &surfaceTexture);
        if (surfaceTexture.texture == null) {
            return new RenderFrame(default, null, surfaceTexture.status, null);  //   surfaceTexture.texture == null   if window minimized
        }
        var handle = wgpuTextureCreateView(surfaceTexture.texture, null);
        var view = new GpuTextureView(handle, null);
        
        return new RenderFrame(view, surfaceTexture.texture, surfaceTexture.status, recorder);
    }
}

/// <summary> see: <see cref="RenderPassDescriptor"/> </summary>
public struct WgpuRenderPassDescriptor
{
    public  WgpuRenderPassColorAttachment[]         colorAttachments;
    public  WgpuRenderPassDepthStencilAttachment?   depthStencilAttachment;
}

/// <summary> see: <see cref="RenderPassColorAttachment"/> </summary>
public struct WgpuRenderPassColorAttachment
{
    public  nint            nextInChain;
    public  GpuTextureView  view;
    public  uint            depthSlice = 0xFFFFFFFF; // 0xFFFFFFFF = WGPU_DEPTH_SLICE_UNDEFINED. Prevent wgpu expects 3D Texture
    public  GpuTextureView  resolveTarget;
    public  LoadOp          loadOp;
    public  StoreOp         storeOp;
    public  WgpuColor       clearValue;
    
    public WgpuRenderPassColorAttachment() { } 
    
    public unsafe RenderPassColorAttachment GetNative()
    {
        return new RenderPassColorAttachment {
            nextInChain     = (ChainedStruct*)nextInChain,
            view            = view.handle,
            depthSlice      = depthSlice,
            resolveTarget   = resolveTarget.handle,
            loadOp          = loadOp,
            storeOp         = storeOp,
            clearValue      = new Color {
                r   = clearValue.r,
                g   = clearValue.g,
                b   = clearValue.b,
                a   = clearValue.a
            }
        };
    }
}


/// <summary> see: <see cref="RenderPassDepthStencilAttachment"/> </summary>
public struct WgpuRenderPassDepthStencilAttachment
{
    public  nint            nextInChain;
    public  GpuTextureView  view;
    public  LoadOp          depthLoadOp;
    public  StoreOp         depthStoreOp;
    public  float           depthClearValue;
    public  uint            depthReadOnly;
    public  LoadOp          stencilLoadOp;
    public  StoreOp         stencilStoreOp;
    public  uint            stencilClearValue;
    public  uint            stencilReadOnly;
    
    public unsafe RenderPassDepthStencilAttachment GetNative()
    {
        return new RenderPassDepthStencilAttachment {
            nextInChain         =  (ChainedStruct*)nextInChain,
            view                =  view.handle,
            depthLoadOp         =  depthLoadOp,
            depthStoreOp        =  depthStoreOp,
            depthClearValue     =  depthClearValue,
            depthReadOnly       =  depthReadOnly,
            stencilLoadOp       =  stencilLoadOp,
            stencilStoreOp      =  stencilStoreOp,
            stencilClearValue   =  stencilClearValue,
            stencilReadOnly     =  stencilReadOnly
        };
    }
}

[CollectionBuilder(typeof(WgpuColorBuilder), nameof(WgpuColorBuilder.Create))]
public struct WgpuColor : IEnumerable<double>
{
    public  double  r;
    public  double  g;
    public  double  b;
    public  double  a;
    
    public IEnumerator<double> GetEnumerator() => throw new NotImplementedException();
    IEnumerator    IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Compiler helper to enable the [...] collection expression for <see cref="WgpuColor"/>.
/// </summary>
public static class WgpuColorBuilder
{
    public static WgpuColor Create(ReadOnlySpan<double> items)
    {
        if (items.Length != 4) throw new ArgumentException("WgpuColor expects 4 elements: [r,g,b,a]");
        var color = new WgpuColor {
            r = items[0],
            g = items[1],
            b = items[2],
            a = items[3]
        };
        return color;
    }
}


public readonly unsafe ref struct  RenderFrame : IDisposable
{
    public   readonly   SurfaceGetCurrentTextureStatus  TextureStatus;
    public   readonly   GpuTextureView                  View;
    private  readonly   CommandRecorder                 recorder;
    private  readonly   Texture*                        surfaceTexture;
    
    public              bool                            IsNull      => recorder == null;

    public   override   string                          ToString()  => TextureStatus.ToString(); 

    internal RenderFrame(GpuTextureView view, Texture* surfaceTexture, SurfaceGetCurrentTextureStatus status, CommandRecorder recorder) {
        View                = view;
        this.surfaceTexture = surfaceTexture;
        TextureStatus       = status;
        this.recorder       = recorder;
    }

    // BindGroup 0 = Stage globals (Camera, Light) - bound ONCE per pass.
    // BindGroup 1 = Shader-specifics (Textures, Materials) - swapped per draw.
    // Minimizes CPU-to-GPU state change overhead dramatically.
    // GPU IMPACT: Guarantees L1/L2 cache residency for global uniform data across the entire pass and eliminates costly hardware pipeline stalls.
    public RenderPass BeginRenderPass(in WgpuRenderPassDescriptor descriptor)
    {
        if (recorder == null) {
            throw new InvalidOperationException("RenderFrame is null");
        }
        if (recorder.currentEncoder.handle == null) {
            recorder.Init(0, "RenderEncoder"u8);		// TODO fix this hack
        }
        
        Span<RenderPassColorAttachment> colorAttachments = stackalloc RenderPassColorAttachment[descriptor.colorAttachments.Length];

        for (int n = 0; n < colorAttachments.Length; n++) {
            colorAttachments[n] = descriptor.colorAttachments[n].GetNative();
            if (colorAttachments[n].view == null) throw new ArgumentException($"renderPassDescriptor.colorAttachments[{n}].view is null. Assign: RenderFrame.View");
        }
        RenderPassDepthStencilAttachment* pDepthStencilAttachment = null;
        RenderPassDepthStencilAttachment   depthStencilAttachment;
        if (descriptor.depthStencilAttachment != null) {
            depthStencilAttachment  = descriptor.depthStencilAttachment.Value.GetNative();
            pDepthStencilAttachment = &depthStencilAttachment;
        }
        fixed (RenderPassColorAttachment* pAttachments = colorAttachments) {
            var renderPassDesc = new RenderPassDescriptor {
                colorAttachmentCount    = (uint)colorAttachments.Length,
                colorAttachments        = pAttachments,
                depthStencilAttachment  = pDepthStencilAttachment
            };
            var passEncoder = wgpuCommandEncoderBeginRenderPass(recorder.currentEncoder.handle, &renderPassDesc);
            return new RenderPass(passEncoder, recorder);
        }
    }
    
    public void Dispose()
    {
        if (View.handle != null) {
            wgpuTextureViewRelease(View.handle);
        }
        if (surfaceTexture != null) {
            wgpuTextureRelease(surfaceTexture);
        }
    }
}

public readonly unsafe ref struct RenderPass : IDisposable
{
    private  readonly   CommandRecorder     Recorder;
    private  readonly   RenderPassEncoder*  handle;
    
    internal RenderPass(RenderPassEncoder* handle, CommandRecorder recorder) {
        this.handle = handle;
        Recorder    = recorder;

    }
    
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public RenderPassInternal Internal => new (handle, Recorder);
    
    public void Dispose()
    {
        if (handle != null) {
            Recorder.Reset();
            wgpuRenderPassEncoderEnd(handle);
            wgpuRenderPassEncoderRelease(handle);
        }
    }
}
