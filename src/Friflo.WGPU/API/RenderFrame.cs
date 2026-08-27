// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Diagnostics;
using Friflo.GPU;
using Friflo.WGPU.Runtime;
using static Friflo.WGPU.Runtime.WebGPU_native;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable TooWideLocalVariableScope
// ReSharper disable CheckNamespace
namespace Friflo.WGPU;


public static partial class WgpuExtensions
{
    extension(PipelineContext context)
    {
        public unsafe RenderTarget BeginRenderTarget(WgpuSurface surface, GpuExtent3D targetSize, ReadOnlySpan<byte> encoderLabel)
        {
            if (surface.handle == null) {
                throw new InvalidOperationException("WgpuSurface is null");
            }
            var recorder = (CommandRecorder)context;
            if (recorder.currentEncoder.handle != null) {
                throw new InvalidOperationException("PipelineContext has already a command encoder. Ensure calling context.Queue.Submit() before");
            }
            SurfaceTexture surfaceTexture;
            wgpuSurfaceGetCurrentTexture(surface.handle, &surfaceTexture);
            if (surfaceTexture.texture == null) {
                // surfaceTexture.texture == null   if window minimized
                return new RenderTarget(default, null, surfaceTexture.status, null, targetSize);
            }
            var handle = wgpuTextureCreateView(surfaceTexture.texture, null);
            var view = new GpuTextureView(null, (nint)handle);
        
            fixed (byte* labelPtr = encoderLabel) {
                var label = WgpuUtils.FromPtrSpan(labelPtr, encoderLabel);
                recorder.currentEncoder = recorder.Device.CreateEncoder(label);
            }
            return new RenderTarget(view, surfaceTexture.texture, surfaceTexture.status, recorder, targetSize);
        }

        public unsafe RenderTarget BeginRenderTarget(GpuTextureView textureView, ReadOnlySpan<byte> encoderLabel)
        {
            var recorder = (CommandRecorder)context;
            if (recorder.currentEncoder.handle != null) {
                throw new InvalidOperationException("PipelineContext has already a command encoder. Ensure calling context.Queue.Submit() before");
            }
            fixed (byte* labelPtr = encoderLabel) {
                var label = WgpuUtils.FromPtrSpan(labelPtr, encoderLabel);
                recorder.currentEncoder = recorder.Device.CreateEncoder(label);
            }
            var size = textureView.texture.Descriptor.size;
            return new RenderTarget(textureView, textureView.texture.handle, SurfaceGetCurrentTextureStatus.SuccessOptimal, recorder, size);
        }
    }
}

/// <summary> see: <see cref="RenderPassDescriptor"/> </summary>
public struct GpuRenderPassDescriptor
{
    public  nint                                    nextInChain;
    public  string                                  label;
    public  GpuRenderPassColorAttachment[]          colorAttachments;
    public  GpuRenderPassDepthStencilAttachment?	depthStencilAttachment;
}

/// <summary> see: <see cref="RenderPassColorAttachment"/> </summary>
public struct GpuRenderPassColorAttachment
{
    public  nint            nextInChain;
    public  GpuTextureView  view;
    public  uint            depthSlice = 0xFFFFFFFF; // 0xFFFFFFFF = WGPU_DEPTH_SLICE_UNDEFINED. Prevent wgpu expects 3D Texture
    public  GpuTextureView  resolveTarget;
    public  LoadOp          loadOp;
    public  StoreOp         storeOp;
    public  GpuColor        clearValue;
    
    public GpuRenderPassColorAttachment() { } 
    
    public unsafe RenderPassColorAttachment GetNative()
    {
        return new RenderPassColorAttachment {
            nextInChain     = (ChainedStruct*)nextInChain,
            view            = view.handle,
            depthSlice      = depthSlice,
            resolveTarget   = resolveTarget.handle,
            loadOp          = loadOp,
            storeOp         = storeOp,
            clearValue      = clearValue.GetNative()
        };
    }
}


/// <summary> see: <see cref="RenderPassDepthStencilAttachment"/> </summary>
public struct GpuRenderPassDepthStencilAttachment
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

// [CollectionBuilder(typeof(GpuColorBuilder), nameof(GpuColorBuilder.Create))]
public struct GpuColor
{
    public  double  r;
    public  double  g;
    public  double  b;
    public  double  a;
    
    internal readonly Color GetNative() => new Color { r = r, g = g, b = b, a = a };
    
    public GpuColor(double  r, double  g, double  b, double  a)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
    }
    
    // public ReadOnlySpan<double>.Enumerator  GetEnumerator() => AsSpan().GetEnumerator();
    // public readonly ReadOnlySpan<double>    AsSpan()        => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in r), 4);
}

/*    
/// <summary>
/// Compiler helper to enable the [...] collection expression for <see cref="GpuColor"/>.
/// </summary>
public static class GpuColorBuilder
{
    public static GpuColor Create(ReadOnlySpan<double> items)
    {
        if (items.Length != 4) throw new ArgumentException("GpuColor expects 4 elements: [r,g,b,a]");
        return new GpuColor {
            r = items[0],
            g = items[1],
            b = items[2],
            a = items[3]
        };
    }
} */

public readonly unsafe ref struct  RenderTarget : IDisposable
{
    public   readonly   SurfaceGetCurrentTextureStatus  TextureStatus;
    public   readonly   GpuTextureView                  View;
    public   readonly   GpuExtent3D                     TargetSize;
    private  readonly   CommandRecorder                 recorder;
    private  readonly   Texture*                        surfaceTexture;
    
    public              PipelineContext                 ComputeContext { [DebuggerStepThrough] get => recorder; }
    public              GpuDevice                       Device          => recorder.Device;
    public              bool                            IsNull          => recorder == null;
    public              int                             Width           => TargetSize.width;
    public              int                             Height          => TargetSize.height;

    public   override   string                          ToString()      => TextureStatus.ToString(); 

    internal RenderTarget(GpuTextureView view, Texture* surfaceTexture, SurfaceGetCurrentTextureStatus status, CommandRecorder recorder, GpuExtent3D targetSize) {
        View                = view;
        this.surfaceTexture = surfaceTexture;
        TextureStatus       = status;
        this.recorder       = recorder;
        TargetSize          = targetSize;
    }

    // BindGroup 0 = Stage globals (Camera, Light) - bound ONCE per pass.
    // BindGroup 1 = Shader-specifics (Textures, Materials) - swapped per draw.
    // Minimizes CPU-to-GPU state change overhead dramatically.
    // GPU IMPACT: Guarantees L1/L2 cache residency for global uniform data across the entire pass and eliminates costly hardware pipeline stalls.
    public RenderPass BeginRenderPass(in GpuRenderPassDescriptor descriptor)
    {
        if (recorder == null) {
            throw new InvalidOperationException("RenderFrame is null");
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
        int     labelMaxCount   = WgpuUtils.GetMaxCount(descriptor.label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        var     label           = WgpuUtils.CopyToStringView(descriptor.label, labelBuffer, labelMaxCount);
        
        var renderPassDesc = new RenderPassDescriptor {
            nextInChain             = (ChainedStruct*)descriptor.nextInChain,
            label                   = label,
            colorAttachmentCount    = (uint)colorAttachments.Length,
            depthStencilAttachment  = pDepthStencilAttachment
        };
        recorder.FinishPass(); // finish compute pass if still open
        
        fixed (RenderPassColorAttachment* pAttachments = colorAttachments) {
            renderPassDesc.colorAttachments = pAttachments;
            var handle = wgpuCommandEncoderBeginRenderPass(recorder.currentEncoder.handle, &renderPassDesc);
            return new RenderPass(handle, recorder, TargetSize);
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

