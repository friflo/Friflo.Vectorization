// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Friflo.GPU;
using Friflo.ImGui;
using Friflo.WGPU.Runtime;
using Shaders.Imdraw;


// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.WGPU.ImGui;

// Note: Copied from generated copy to avoid build deadlock in case of generator issues.
public partial class WgpuBatch
{
    private static partial void Draw(
        RenderPass                  pass,
        RenderConfig                config,
        in ImUniforms               globals,
        GpuTextureView              texture,
        GpuSampler                  sampler,
        InBuffer<Vertex2D>          vertices,
        InBuffer<uint>              indices)
    {

        var pass_       = pass.Internal;
        var recorder    = pass_.Recorder;
        recorder.InitShader(_Draw_GPU_ShaderId);

        recorder.RequireRead     (vertices);
        recorder.RequireRead     (indices);
        
        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(_Draw_GPU_ShaderId, config, _Draw_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref _Draw_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (_Draw_GPU_Cache)pipelineCache.bindGroupCache;

        // --- bind group 0
        var key_0 = (texture.Handle, sampler.Handle);
        if (!bindGroupCache.bindGroup_0.TryGetValue(key_0, out var bindGroup_0)) {
            recorder.BindGroupEntryUniform<ImUniforms>(0);
            recorder.BindGroupEntryTexture(1, texture);
            recorder.BindGroupEntrySampler(2, sampler);
            bindGroup_0 = recorder.CreateBindGroup(pipelineCache.layouts[0], "Draw_bindGroup_0"u8);
            bindGroupCache.bindGroup_0.Add(key_0, bindGroup_0);
        }
        pass_.AddUniform(globals);
        pass_.SetBindGroupUniforms(0, bindGroup_0);
        
        pass_.SetVertexBuffer(vertices, 0);
        pass_.SetIndexBuffer(indices, IndexFormat.Uint32);
        
        // --- draw
        pass_.DrawIndexed(indices, new DrawArgs());
    }

    private sealed class _Draw_GPU_Cache : BindGroupCache
    {
        internal readonly   Dictionary<(nint, nint), WgpuBindGroup> bindGroup_0 = new ();

        protected internal override void Clear() {
            ReleaseBindGroups(bindGroup_0);
        }
    }

    private static readonly int _Draw_GPU_ShaderId            =  ShaderRegistry.NewShaderId("Draw");
    private const  ulong        _Draw_GPU_layout_0_Key        =  0xc1dee3af2208a286;

    private static ulong        _Draw_GPU_WgslHash            => 0xda580dc7dfa46e13UL;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache _Draw_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[1];
        var layout_0 = device.GetBindGroupLayout(_Draw_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutUniform(0);
            device.BindGroupLayoutTexture(1, TextureSampleType.Float, TextureViewDimension.D2D, false);
            device.BindGroupLayoutSampler(2, SamplerBindingType.Filtering);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _Draw_GPU_layout_0_Key, "Draw_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(ImGuiBackend), _Draw_GPU_Shaders, "Draw_pipeline"u8);

        var bindGroupCache = new _Draw_GPU_Cache();
        return ref device.CreatePipelineCache(_Draw_GPU_ShaderId, config, _Draw_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] _Draw_GPU_Shaders = [
        new("shaders/imdraw/draw2d.wgsl", vert: "vs_main", frag: "fs_main"),
    ];

}