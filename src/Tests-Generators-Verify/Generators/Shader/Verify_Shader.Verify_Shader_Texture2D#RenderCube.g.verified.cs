//HintName: VerifyShader/ShaderExample/RenderCube.g.cs
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
    protected static partial void RenderCube(
        RenderPass                  pass,
        RenderConfig                config,
        in Uniforms                 uniforms,
        GpuSampler                  smoothFilter,
        GpuTextureView              material,
        InBuffer<float>             vertices)
    {

        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(_RenderCube_GPU_ShaderId, "RenderCube_encoder"u8);

        recorder.RequireRead     (vertices);
        
        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(_RenderCube_GPU_ShaderId, config, _RenderCube_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref _RenderCube_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (_RenderCube_GPU_Cache)pipelineCache.bindGroupCache;

        // --- bind group 0
        var key_0 = (smoothFilter.Handle, material.Handle);
        if (!bindGroupCache.bindGroup0.TryGetValue(key_0, out var bindGroup0)) {
            recorder.BindGroupEntryUniform<Uniforms>();
            recorder.BindGroupEntrySampler(smoothFilter);
            recorder.BindGroupEntryTexture(material);
            bindGroup0 = recorder.CreateBindGroup(pipelineCache.layouts[0], "RenderCube_bindGroup0"u8);
            bindGroupCache.bindGroup0.Add(key_0, bindGroup0);
        }
        pass_.AddUniform(uniforms);
        pass_.SetBindGroupUniforms(0, bindGroup0);
        
        pass_.SetVertexBuffer(vertices, 0);
        
        // --- draw
        pass_.Draw(vertices, 0, config, 1, 0, 0);
    }

    private sealed class _RenderCube_GPU_Cache : BindGroupCache
    {
        internal readonly   Dictionary<(nint, nint), WgpuBindGroup>    bindGroup0 = new ();

        protected override void Clear() {
            ReleaseBindGroups(bindGroup0);
        }
    }

    private static readonly int _RenderCube_GPU_ShaderId            =  ShaderRegistry.NewShaderId("RenderCube");
    private const  ulong        _RenderCube_GPU_layout_0_Key        =  0x7cdb547530a9203a;

    private static ulong        _RenderCube_GPU_WgslHash            => 0x7d9a5dec1e37d625UL;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache _RenderCube_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[1];
        var layout_0 = device.GetBindGroupLayout(_RenderCube_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutUniform();
            device.BindGroupLayoutSampler(SamplerBindingType.Filtering);
            device.BindGroupLayoutTexture(TextureSampleType.Float, TextureViewDimension.D2D, false);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _RenderCube_GPU_layout_0_Key, "RenderCube_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(ShaderExample), _RenderCube_GPU_Shaders, "RenderCube_pipeline"u8);

        var bindGroupCache = new _RenderCube_GPU_Cache();
        return ref device.CreatePipelineCache(_RenderCube_GPU_ShaderId, config, _RenderCube_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] _RenderCube_GPU_Shaders = [
        new WgpuShader("shaders/basic.vert.wgsl", vert: "main"),
        new WgpuShader("shaders/sampleTextureMixColor.frag.wgsl", frag: "main"),
    ];

}