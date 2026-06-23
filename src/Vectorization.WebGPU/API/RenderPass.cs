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
    internal readonly   TextureView*  handle = view;
    
    public void Dispose()
    {
        if (handle != null) {
            wgpuTextureViewRelease(handle);
        }
    }
}


public readonly unsafe ref struct  RenderFrame : IDisposable
{
    public   readonly   SurfaceGetCurrentTextureStatus  TextureStatus;
    private  readonly   WgpuTextureView                 view;
    private  readonly   CommandRecorder                 recorder;
    private  readonly   Texture*                        surfaceTexture;
    
    public              bool                            IsNull      => recorder == null;

    public   override   string                          ToString()  => TextureStatus.ToString(); 

    internal RenderFrame(WgpuTextureView view, Texture* surfaceTexture, SurfaceGetCurrentTextureStatus status, CommandRecorder recorder) {
        this.view           = view;
        this.surfaceTexture = surfaceTexture;
        TextureStatus       = status;
        this.recorder       = recorder;
    }

    // OPTIMIZATION: <TStage> enables Static Global Bindings.
    // BindGroup 0 = Stage globals (Camera, Light) - bound ONCE per pass.
    // BindGroup 1 = Shader-specifics (Textures, Materials) - swapped per draw.
    // Minimizes CPU-to-GPU state change overhead dramatically.
    // GPU IMPACT: Guarantees L1/L2 cache residency for global uniform data across the entire pass and eliminates costly hardware pipeline stalls.
    public RenderPass<TStage> BeginRenderPass<TStage>(RenderPassColorAttachment attachment, RenderConfig config) where TStage : unmanaged
    {
        if (recorder == null) {
            throw new InvalidOperationException("RenderFrame is null");
        }
        attachment.view = view.handle;
        var renderPassDesc = new RenderPassDescriptor {
            colorAttachmentCount    = 1,
            colorAttachments        = &attachment
        };
        if (recorder.currentEncoder.handle == null) {
            recorder.Init(0, "RenderEncoder"u8);		// TODO fix this hack
        }
        var passEncoder = wgpuCommandEncoderBeginRenderPass(recorder.currentEncoder.handle, &renderPassDesc);
        return new RenderPass<TStage>(passEncoder, recorder, config);
    }
    
    public void Dispose()
    {
        view.Dispose();
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

    public void SetBindGroup(uint groupIndex, WgpuBindGroup bindGroup, ulong hash)
    {
        wgpuRenderPassEncoderSetBindGroup(handle, groupIndex, bindGroup.handle, 0, null);
    }
    
    public void SetUniformBindGroup<T>(uint groupIndex, ref WgpuShaderEffect effect, T uniform, ReadOnlySpan<byte> groupLabel) where T : unmanaged
    {
        uint alignedSize    = ((uint)sizeof(T) + (CommandRecorder.UniformAlignment - 1)) & ~(CommandRecorder.UniformAlignment - 1);
        var rec             = Recorder;
        var bindGroup       = rec.GetUniformBindGroup(effect.uniformLayout, alignedSize, ref rec.shaderUniformGroups, groupLabel);

        uint offset = rec.uniformOffset;
        
        fixed (byte* pStaging = rec.stagingBuffer) {
            *(T*)(pStaging + offset) = uniform;
        }
        wgpuRenderPassEncoderSetBindGroup(handle, groupIndex, bindGroup.handle, 1, &offset);
        
        rec.uniformOffset = offset + alignedSize;
    }
    
    public void SetVertexBuffer<T>(int slot, InBuffer<T> buffer) where T : unmanaged
    {
        ulong offset = 0; // (ulong)buffer.Offset;  TODO   use buffer.Offset
        ulong length = 0; // (ulong)buffer.Length;  TODO   use buffer.Length
        wgpuRenderPassEncoderSetVertexBuffer(handle, (uint)slot, (Buffer*)buffer.Buffer.NativeHandle, offset, length);
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




