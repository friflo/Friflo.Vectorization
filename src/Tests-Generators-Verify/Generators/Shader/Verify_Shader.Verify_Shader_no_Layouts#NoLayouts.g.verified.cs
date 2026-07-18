//HintName: VerifyShader/ShaderExample/NoLayouts.g.cs
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
    public static partial void NoLayouts(
        RenderPass                  pass,
        RenderConfig                config)
    {

        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(_NoLayouts_GPU_ShaderId, "NoLayouts_encoder"u8);

        
        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(_NoLayouts_GPU_ShaderId, config, _NoLayouts_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref _NoLayouts_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (_NoLayouts_GPU_Cache)pipelineCache.bindGroupCache;

        // --- draw
    }

    private sealed class _NoLayouts_GPU_Cache : BindGroupCache
    {

        protected override void Clear() {
        }
    }

    private static readonly int _NoLayouts_GPU_ShaderId            =  ShaderRegistry.NewShaderId("NoLayouts");

    private static ulong        _NoLayouts_GPU_WgslHash            => 0x3f0b7c32bbfa9a08UL;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache _NoLayouts_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[0];
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(ShaderExample), _NoLayouts_GPU_Shaders, "NoLayouts_pipeline"u8);

        var bindGroupCache = new _NoLayouts_GPU_Cache();
        return ref device.CreatePipelineCache(_NoLayouts_GPU_ShaderId, config, _NoLayouts_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] _NoLayouts_GPU_Shaders = [
        new("tests/noBindings.wgsl", vert: "vs_main", frag: "fs_main"),
    ];

}