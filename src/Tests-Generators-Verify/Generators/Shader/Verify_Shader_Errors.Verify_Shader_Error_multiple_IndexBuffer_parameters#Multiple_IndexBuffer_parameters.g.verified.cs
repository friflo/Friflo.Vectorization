//HintName: VerifyShader/ShaderExample/Multiple_IndexBuffer_parameters.g.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

namespace VerifyShader;

public partial class ShaderExample
{
    protected static partial void Multiple_IndexBuffer_parameters(
        RenderPass                  pass,
        RenderConfig                config,
        InBuffer<ushort>            indexBuffer1,
        InBuffer<ushort>            indexBuffer2)
    {

        var pass_       = pass.Internal;
        var recorder    = pass_.Recorder;
        recorder.InitShader(_Multiple_IndexBuffer_parameters_GPU_ShaderId);

        recorder.RequireRead     (indexBuffer1);
        recorder.RequireRead     (indexBuffer2);
        
        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(_Multiple_IndexBuffer_parameters_GPU_ShaderId, config, _Multiple_IndexBuffer_parameters_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref _Multiple_IndexBuffer_parameters_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (_Multiple_IndexBuffer_parameters_GPU_Cache)pipelineCache.bindGroupCache;

        pass_.SetIndexBuffer(indexBuffer1, IndexFormat.Uint16);
        pass_.SetIndexBuffer(indexBuffer2, IndexFormat.Uint16);
        
        // --- draw
        pass_.DrawIndexed(indexBuffer1, new DrawArgs());
    }

    private sealed class _Multiple_IndexBuffer_parameters_GPU_Cache : BindGroupCache
    {

        protected override void Clear() {
        }
    }

    private static readonly int _Multiple_IndexBuffer_parameters_GPU_ShaderId            =  ShaderRegistry.NewShaderId("Multiple_IndexBuffer_parameters");

    private static ulong        _Multiple_IndexBuffer_parameters_GPU_WgslHash            => 0x259828d805e43104UL;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache _Multiple_IndexBuffer_parameters_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[0];
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(ShaderExample), _Multiple_IndexBuffer_parameters_GPU_Shaders, "Multiple_IndexBuffer_parameters_pipeline"u8);

        var bindGroupCache = new _Multiple_IndexBuffer_parameters_GPU_Cache();
        return ref device.CreatePipelineCache(_Multiple_IndexBuffer_parameters_GPU_ShaderId, config, _Multiple_IndexBuffer_parameters_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] _Multiple_IndexBuffer_parameters_GPU_Shaders = [
        new("shaders/renderTest/triangle.wgsl", vert: "vs_main", frag: "fs_main"),
    ];

}