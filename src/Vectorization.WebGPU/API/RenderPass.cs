// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;
using Buffer = Friflo.Vectorization.WebGPU.Runtime.Buffer;

// ReSharper disable UnassignedField.Global
// ReSharper disable TooWideLocalVariableScope
// ReSharper disable UnusedTypeParameter
// ReSharper disable InvertIf
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
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
        var view = new WgpuTextureView(handle);
        
        return new RenderFrame(view, surfaceTexture.texture, surfaceTexture.status, recorder);
    }
}


public readonly unsafe struct WgpuTextureView(TextureView* view) : IDisposable
{
    internal readonly   TextureView*    handle  = view;
    
    public void Dispose()
    {
        if (handle != null) {
            wgpuTextureViewRelease(handle);
        }
    }
}

/// <summary> see: <see cref="RenderPassDescriptor"/> </summary>
public struct RenderPassOptions
{
    public  WgpuRenderPassColorAttachment[]         colorAttachments;
    public  WgpuRenderPassDepthStencilAttachment?   depthStencilAttachment;
}

/// <summary> see: <see cref="RenderPassColorAttachment"/> </summary>
public struct WgpuRenderPassColorAttachment
{
    public  nint            nextInChain;
    public  WgpuTextureView view;
    public  uint            depthSlice;
    public  WgpuTextureView resolveTarget;
    public  LoadOp          loadOp;
    public  StoreOp         storeOp;
    public  Color           clearValue;
    
    public unsafe RenderPassColorAttachment GetNative()
    {
        return new RenderPassColorAttachment {
            nextInChain     = (ChainedStruct*)nextInChain,
            view            = view.handle,
            depthSlice      = depthSlice,
            resolveTarget   = resolveTarget.handle,
            loadOp          = loadOp,
            storeOp         = storeOp,
            clearValue      = clearValue
        };
    }
}


/// <summary> see: <see cref="RenderPassDepthStencilAttachment"/> </summary>
public struct WgpuRenderPassDepthStencilAttachment
{
    public  nint    nextInChain;
    public  nint    view;
    public  LoadOp  depthLoadOp;
    public  StoreOp depthStoreOp;
    public  float   depthClearValue;
    public  uint    depthReadOnly;
    public  LoadOp  stencilLoadOp;
    public  StoreOp stencilStoreOp;
    public  uint    stencilClearValue;
    public  uint    stencilReadOnly;
    
    public unsafe RenderPassDepthStencilAttachment GetNative()
    {
        return new RenderPassDepthStencilAttachment {
            nextInChain         =  (ChainedStruct*)nextInChain,
            view                =  (TextureView*)view,
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


public readonly unsafe ref struct  RenderFrame : IDisposable
{
    public   readonly   SurfaceGetCurrentTextureStatus  TextureStatus;
    public   readonly   WgpuTextureView                 View;
    private  readonly   CommandRecorder                 recorder;
    private  readonly   Texture*                        surfaceTexture;
    
    public              bool                            IsNull      => recorder == null;

    public   override   string                          ToString()  => TextureStatus.ToString(); 

    internal RenderFrame(WgpuTextureView view, Texture* surfaceTexture, SurfaceGetCurrentTextureStatus status, CommandRecorder recorder) {
        View                = view;
        this.surfaceTexture = surfaceTexture;
        TextureStatus       = status;
        this.recorder       = recorder;
    }


    /* public RenderPass<TStage> BeginRenderPass<TStage>(RenderPassColorAttachment attachment, RenderConfig config) where TStage : unmanaged
    {
        if (recorder == null) {
            throw new InvalidOperationException("RenderFrame is null");
        }
        if (recorder.currentEncoder.handle == null) {
            recorder.Init(0, "RenderEncoder"u8);
        }
        attachment.view = view.handle;
        var renderPassDesc = new RenderPassDescriptor {
            colorAttachmentCount    = 1,
            colorAttachments        = &attachment
        };
        var passEncoder = wgpuCommandEncoderBeginRenderPass(recorder.currentEncoder.handle, &renderPassDesc);
        return new RenderPass<TStage>(passEncoder, recorder, config);
    } */
    
    // OPTIMIZATION: <TStage> enables Static Global Bindings.
    // BindGroup 0 = Stage globals (Camera, Light) - bound ONCE per pass.
    // BindGroup 1 = Shader-specifics (Textures, Materials) - swapped per draw.
    // Minimizes CPU-to-GPU state change overhead dramatically.
    // GPU IMPACT: Guarantees L1/L2 cache residency for global uniform data across the entire pass and eliminates costly hardware pipeline stalls.
    public RenderPass<TStage> BeginRenderPass<TStage>(in RenderPassOptions options, RenderConfig config) where TStage : unmanaged
    {
        if (recorder == null) {
            throw new InvalidOperationException("RenderFrame is null");
        }
        if (recorder.currentEncoder.handle == null) {
            recorder.Init(0, "RenderEncoder"u8);		// TODO fix this hack
        }
        
        Span<RenderPassColorAttachment> colorAttachments = stackalloc RenderPassColorAttachment[options.colorAttachments.Length];

        for (int n = 0; n < colorAttachments.Length; n++) {
            colorAttachments[n] = options.colorAttachments[n].GetNative();
        }
        RenderPassDepthStencilAttachment* pDepthStencilAttachment = null;
        RenderPassDepthStencilAttachment   depthStencilAttachment;
        if (options.depthStencilAttachment != null) {
            depthStencilAttachment  = options.depthStencilAttachment.Value.GetNative();
            pDepthStencilAttachment = &depthStencilAttachment;
        }
        fixed (RenderPassColorAttachment* pAttachments = colorAttachments) {
            var renderPassDesc = new RenderPassDescriptor {
                colorAttachmentCount    = (uint)colorAttachments.Length,
                colorAttachments        = pAttachments,
                depthStencilAttachment  = pDepthStencilAttachment
            };
            var passEncoder = wgpuCommandEncoderBeginRenderPass(recorder.currentEncoder.handle, &renderPassDesc);
            return new RenderPass<TStage>(passEncoder, recorder, config);
        }
    }
    
    public void Dispose()
    {
        View.Dispose();
        if (surfaceTexture != null) {
            wgpuTextureRelease(surfaceTexture);
        }
    }
}

public readonly unsafe ref  struct RenderPass<TStage> : IDisposable where TStage : unmanaged
{
    private  readonly   CommandRecorder     Recorder;
    private  readonly   RenderPassEncoder*  handle;
    private  readonly   RenderConfig        config;
    
    internal RenderPass(RenderPassEncoder* handle, CommandRecorder recorder, RenderConfig config) {
        this.handle = handle;
        Recorder    = recorder;
        this.config = config;
    }
    
    public RenderPass Value => new RenderPass(handle, Recorder, config);
    
    public void Dispose()
    {
        if (handle != null) {
            Recorder.Reset();
            wgpuRenderPassEncoderEnd(handle);
            wgpuRenderPassEncoderRelease(handle);
        }
    }
}

public readonly unsafe ref  struct RenderPass
{
    public   readonly   CommandRecorder       Recorder;
    private  readonly   RenderPassEncoder*    handle;
    public   readonly   RenderConfig          Config;
    
    internal RenderPass(RenderPassEncoder* handle, CommandRecorder recorder, RenderConfig config) {
        this.handle     = handle;
        this.Recorder   = recorder;
        Config          = config;
    }

    public void SetPipeline(WgpuRenderPipeline renderPipeline)
    {
        wgpuRenderPassEncoderSetPipeline(handle, renderPipeline.handle);
    }

    public void SetBindGroup(uint groupIndex, WgpuBindGroup bindGroup)
    {
        wgpuRenderPassEncoderSetBindGroup(handle, groupIndex, bindGroup.handle, 0, null);
    }
    
    /// <summary> Set bind group with a uniform for a group layout with multiple layout single entries. </summary>
    public void SetBindGroupUniform<T>(uint groupIndex, WgpuBindGroup bindGroup, T uniform) where T : unmanaged
    {
        uint alignedSize    = ((uint)sizeof(T) + (CommandRecorder.UniformAlignment - 1)) & ~(CommandRecorder.UniformAlignment - 1);
        var rec             = Recorder;
        uint offset         = rec.uniformOffset;
        
        fixed (byte* pStaging = rec.stagingBuffer) {
            *(T*)(pStaging + offset) = uniform;
        }
        wgpuRenderPassEncoderSetBindGroup(handle, groupIndex, bindGroup.handle, 1, &offset);
        rec.uniformOffset = offset + alignedSize;
    }
    
    /// <summary> Set bind group with a uniform for a group layout with only a single layout single entry. </summary>
    public void SetBindGroupUniform<T>(uint groupIndex, ref WgpuBindGroup bindGroup, T uniform, in PipelineCache pipelineCache, ReadOnlySpan<byte> groupLabel) where T : unmanaged
    {
        var rec = Recorder;
        if (!bindGroup.IsCreated) {
            var entry   = rec.CreateUniformBindGroupEntry<T>(0);
            bindGroup   = rec.CreateBindGroupNew(pipelineCache.layouts[(int)groupIndex], entry, groupLabel);
        }
        uint alignedSize    = ((uint)sizeof(T) + (CommandRecorder.UniformAlignment - 1)) & ~(CommandRecorder.UniformAlignment - 1);
        uint offset         = rec.uniformOffset;
        
        fixed (byte* pStaging = rec.stagingBuffer) {
            *(T*)(pStaging + offset) = uniform;
        }
        wgpuRenderPassEncoderSetBindGroup(handle, groupIndex, bindGroup.handle, 1, &offset);
        rec.uniformOffset = offset + alignedSize;
    }

    /// <summary>
    /// See <see cref="VertexBufferAttribute"/> documentation for setting <c>arrayStride</c> in a <see cref="WgpuVertexBufferLayout"/>.  
    /// </summary>
    public int SetVertexBuffer<T>(RenderConfig config, int slot, in InBuffer<T> buffer) where T : unmanaged
    {
        ulong offset = (ulong)(buffer.Offset * sizeof(T)); // size in bytes
        ulong size   = (ulong)(buffer.Length * sizeof(T)); // size in bytes
        int vertexCount = (int)(size / config.Descriptor.VertexState.buffers[slot].arrayStride); // arrayStride == 0 should result in DivideByZeroException  
        
        wgpuRenderPassEncoderSetVertexBuffer(handle, (uint)slot, (Buffer*)buffer.Buffer.NativeHandle, offset, size);
        return vertexCount;
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

public static class WgpuResource
{
    public static unsafe ReadOnlySpan<byte> GetResource(Assembly assembly, string resourceName)
    {
        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) { 
            throw new FileNotFoundException($"Resource '{resourceName}' not found");
        }
        if (stream is UnmanagedMemoryStream unmanagedStream)
        {
            var span = new ReadOnlySpan<byte>(unmanagedStream.PositionPointer, (int)unmanagedStream.Length);
        
            // Detect UTF-8 BOM and skip
            if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF) {
                return span.Slice(3);
            }
            return span;
        }
        throw new InvalidOperationException($"Resource '{resourceName}' not found");
    }
}




