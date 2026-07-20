//HintName: VerifyShader/ShaderExample/TestTextureTypes.g.cs
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
    public static partial void TestTextureTypes(
        RenderPass                  pass,
        RenderConfig                config)
    {

        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(_TestTextureTypes_GPU_ShaderId, "TestTextureTypes_encoder"u8);

        
        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(_TestTextureTypes_GPU_ShaderId, config, _TestTextureTypes_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref _TestTextureTypes_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (_TestTextureTypes_GPU_Cache)pipelineCache.bindGroupCache;

        // --- draw
    }

    private sealed class _TestTextureTypes_GPU_Cache : BindGroupCache
    {

        protected override void Clear() {
        }
    }

    private static readonly int _TestTextureTypes_GPU_ShaderId            =  ShaderRegistry.NewShaderId("TestTextureTypes");

    private static ulong        _TestTextureTypes_GPU_WgslHash            => 0xdfedd3c4778a619cUL;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache _TestTextureTypes_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[0];
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(ShaderExample), _TestTextureTypes_GPU_Shaders, "TestTextureTypes_pipeline"u8);

        var bindGroupCache = new _TestTextureTypes_GPU_Cache();
        return ref device.CreatePipelineCache(_TestTextureTypes_GPU_ShaderId, config, _TestTextureTypes_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] _TestTextureTypes_GPU_Shaders = [
        new("shaders/tests/testTextureTypes.frag.wgsl", frag: "main"),
    ];

}