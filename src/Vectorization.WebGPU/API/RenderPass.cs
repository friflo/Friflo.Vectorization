// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable InvertIf
// ReSharper disable UnusedParameter.Local
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable UnusedTypeParameter
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;

[AttributeUsage(AttributeTargets.Method)]
public sealed class ShaderAttribute<TStage> : Attribute where TStage : struct
{
    public ShaderAttribute (string wgsl) { }
}

public static class WgpuExtensions
{
    public static unsafe RenderFrame BeginFrame(this PipelineContext context, WgpuSurface surface)
    {
        var recorder = (CommandRecorder)context;
        SurfaceTexture surfaceTexture;
        wgpuSurfaceGetCurrentTexture(surface.handle, &surfaceTexture);
        
        var handle = wgpuTextureCreateView(surfaceTexture.texture, null);
        var view = new WgpuTextureView(handle);
        
        return new RenderFrame(view, surfaceTexture.texture, recorder);
    }
}


public readonly unsafe struct WgpuTextureView(TextureView* view) : IDisposable
{
    internal readonly   TextureView*  handle = view;
    
    public void Dispose() {
        wgpuTextureViewRelease(handle);
    }
}


public readonly unsafe struct RenderFrame : IDisposable
{
    private  readonly   WgpuTextureView view;
    private  readonly   CommandRecorder recorder;
    private  readonly   Texture*        surfaceTexture;
    
    internal RenderFrame(WgpuTextureView view, Texture* surfaceTexture, CommandRecorder recorder) {
        this.view           = view;
        this.surfaceTexture = surfaceTexture;
        this.recorder       = recorder;
    }

    // OPTIMIZATION: <TStage> enables Static Global Bindings.
    // BindGroup 0 = Stage globals (Camera, Light) - bound ONCE per pass.
    // BindGroup 1 = Shader-specifics (Textures, Materials) - swapped per draw.
    // Minimizes CPU-to-GPU state change overhead dramatically.
    // GPU IMPACT: Guarantees L1/L2 cache residency for global uniform data across the entire pass and eliminates costly hardware pipeline stalls.
    public RenderPass<TStage> BeginRenderPass<TStage>(RenderPassColorAttachment attachment) where TStage : struct
    {
        attachment.view = view.handle;
        var renderPassDesc = new RenderPassDescriptor {
            colorAttachmentCount    = 1,
            colorAttachments        = &attachment
        };
        if (recorder.currentEncoder.handle == null) {
            recorder.Init(0, "RenderEncoder"u8);		// TODO fix this hack
        }
        var passEncoder = wgpuCommandEncoderBeginRenderPass(recorder.currentEncoder.handle, &renderPassDesc);
        return new RenderPass<TStage>(passEncoder, recorder);
    }
    
    public void Dispose() {
        view.Dispose();
        wgpuTextureRelease(surfaceTexture);
    }
}

public readonly unsafe struct RenderPass<TStage> : IDisposable where TStage : struct
{
    private  readonly   CommandRecorder     Recorder;
    private  readonly   RenderPassEncoder*  handle;
    
    internal RenderPass(RenderPassEncoder* handle, CommandRecorder recorder) {
        this.handle = handle;
        Recorder    = recorder;
    }
    
    public RenderPass Value => new RenderPass(handle, Recorder);
    
    public void Dispose()
    {
        if (handle != null) {
            wgpuRenderPassEncoderEnd(handle);
            wgpuRenderPassEncoderRelease(handle);
        }
    }
}

public readonly unsafe struct RenderPass
{
    public   readonly CommandRecorder       recorder;
    private  readonly RenderPassEncoder*    handle;
    
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
    
    public void SetUniformBindGroup<T>(uint groupIndex, ref WgpuComputeEffect effect, T uniform, ReadOnlySpan<byte> groupLabel) where T : unmanaged
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




