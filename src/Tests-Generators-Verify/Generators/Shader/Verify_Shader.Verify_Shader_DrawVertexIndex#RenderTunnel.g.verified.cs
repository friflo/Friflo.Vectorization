//HintName: VerifyShader/ShaderExample/RenderTunnel.g.cs
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

public partial struct ShaderExample
{
    public static partial void RenderTunnel(
        RenderPass                  pass,
        RenderConfig                config,
        in Uniforms                 uniforms)
    {

        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(_RenderTunnel_GPU_ShaderId, "RenderTunnel_encoder"u8);

        
        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(_RenderTunnel_GPU_ShaderId, config, _RenderTunnel_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref _RenderTunnel_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (_RenderTunnel_GPU_Cache)pipelineCache.bindGroupCache;

        // --- bind group 0
        pass_.SetBindGroupUniform(0, ref bindGroupCache.bindGroup0, uniforms, pipelineCache,"RenderTunnel_bindGroup0"u8);
        
        // --- draw
        pass_.Draw(3, 1, 0, 0);
    }

    private sealed class _RenderTunnel_GPU_Cache : BindGroupCache
    {
        internal            WgpuBindGroup bindGroup0;

        protected override void Clear() {
            ReleaseBindGroup(ref bindGroup0);
        }
    }

    private static readonly int _RenderTunnel_GPU_ShaderId            =  ShaderRegistry.NewShaderId("RenderTunnel");
    private const  ulong        _RenderTunnel_GPU_layout_0_Key        =  0xad2eca77479f2364;

    private static ulong        _RenderTunnel_GPU_WgslHash            => 0x0UL;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache _RenderTunnel_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[1];
        var layout_0 = device.GetBindGroupLayout(_RenderTunnel_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutUniform();
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _RenderTunnel_GPU_layout_0_Key, "RenderTunnel_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(ShaderExample), _RenderTunnel_GPU_Shaders(), "RenderTunnel_pipeline"u8);

        var bindGroupCache = new _RenderTunnel_GPU_Cache();
        return ref device.CreatePipelineCache(_RenderTunnel_GPU_ShaderId, config, _RenderTunnel_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static WgpuShader[] _RenderTunnel_GPU_Shaders() => [
        new WgpuShader("shaders/raymarcher_no_texture.wgsl"),
    ];

}