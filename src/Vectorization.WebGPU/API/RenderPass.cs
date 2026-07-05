// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;
using Buffer = Friflo.Vectorization.WebGPU.Runtime.Buffer;

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ConvertToConstant.Global
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
    public  Color           clearValue;
    
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
            clearValue      = clearValue
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
            bindGroup   = rec.CreateBindGroupInternal(pipelineCache.layouts[(int)groupIndex], entry, groupLabel);
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
    public void SetVertexBuffer<T>(in InBuffer<T> buffer, int slot) where T : unmanaged
    {
        ulong offset = (ulong)(buffer.Offset * sizeof(T)); // size in bytes
        ulong size   = (ulong)(buffer.Length * sizeof(T)); // size in bytes
        
        wgpuRenderPassEncoderSetVertexBuffer(handle, (uint)slot, (Buffer*)buffer.Buffer.NativeHandle, offset, size);
    }
    
    public void Draw<T>(in InBuffer<T> buffer, int slot, RenderConfig config, int instanceCount, int firstVertex, int firstInstance) where T : unmanaged
    {
        ulong size      = (ulong)(buffer.Length * sizeof(T)); // size in bytes
        int vertexCount = (int)(size / config.Descriptor.VertexState.buffers[slot].arrayStride); // arrayStride == 0 should result in DivideByZeroException 
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

public static class WgpuResource
{
     
    public static ReadOnlySpan<byte> GetResource(Type type, string resourceName)
    {
        return GetResource(type.Assembly, resourceName);
    }
     
    private static unsafe ReadOnlySpan<byte> GetResource(Assembly assembly, string resourceName)
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




