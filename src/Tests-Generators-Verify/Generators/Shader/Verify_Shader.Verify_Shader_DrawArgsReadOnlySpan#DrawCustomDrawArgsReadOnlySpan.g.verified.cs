//HintName: VerifyShader/ShaderExample/DrawCustomDrawArgsReadOnlySpan.g.cs
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
    private static partial void DrawCustomDrawArgsReadOnlySpan(
        RenderPass                  pass,
        RenderConfig                config,
        InBuffer<Matrix4x4>         mvpMatrices,
        InBuffer<float>             verticesBuffer,
        ReadOnlySpan<DrawArgs>      customArgs)
    {

        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(_DrawCustomDrawArgsReadOnlySpan_GPU_ShaderId, "DrawCustomDrawArgsReadOnlySpan_encoder"u8);

        recorder.RequireRead     (mvpMatrices);
        recorder.RequireRead     (verticesBuffer);
        
        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(_DrawCustomDrawArgsReadOnlySpan_GPU_ShaderId, config, _DrawCustomDrawArgsReadOnlySpan_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref _DrawCustomDrawArgsReadOnlySpan_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (_DrawCustomDrawArgsReadOnlySpan_GPU_Cache)pipelineCache.bindGroupCache;

        // --- bind group 0
        var key_0 = mvpMatrices.Handle;
        if (!bindGroupCache.bindGroup0.TryGetValue(key_0, out var bindGroup0)) {
            recorder.BindGroupEntryBuffer(0, mvpMatrices.Buffer);
            bindGroup0 = recorder.CreateBindGroup(pipelineCache.layouts[0], "DrawCustomDrawArgsReadOnlySpan_bindGroup0"u8);
            bindGroupCache.bindGroup0.Add(key_0, bindGroup0);
        }
        pass_.SetBindGroup(0, bindGroup0);
        
        pass_.SetVertexBuffer(verticesBuffer, 0);
        
        // --- draw
        foreach(var customArgsItem in customArgs) {
            pass_.Draw(verticesBuffer, 0, config, customArgsItem);
        }
    }

    private sealed class _DrawCustomDrawArgsReadOnlySpan_GPU_Cache : BindGroupCache
    {
        internal readonly   Dictionary<nint, WgpuBindGroup>    bindGroup0 = new ();

        protected override void Clear() {
            ReleaseBindGroups(bindGroup0);
        }
    }

    private static readonly int _DrawCustomDrawArgsReadOnlySpan_GPU_ShaderId            =  ShaderRegistry.NewShaderId("DrawCustomDrawArgsReadOnlySpan");
    private const  ulong        _DrawCustomDrawArgsReadOnlySpan_GPU_layout_0_Key        =  0xad2bca77479a0a64;

    private static ulong        _DrawCustomDrawArgsReadOnlySpan_GPU_WgslHash            => 0x7bea408b45888bf2UL;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache _DrawCustomDrawArgsReadOnlySpan_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[1];
        var layout_0 = device.GetBindGroupLayout(_DrawCustomDrawArgsReadOnlySpan_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutBuffer(0, BufferBindingType.Uniform);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _DrawCustomDrawArgsReadOnlySpan_GPU_layout_0_Key, "DrawCustomDrawArgsReadOnlySpan_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(ShaderExample), _DrawCustomDrawArgsReadOnlySpan_GPU_Shaders, "DrawCustomDrawArgsReadOnlySpan_pipeline"u8);

        var bindGroupCache = new _DrawCustomDrawArgsReadOnlySpan_GPU_Cache();
        return ref device.CreatePipelineCache(_DrawCustomDrawArgsReadOnlySpan_GPU_ShaderId, config, _DrawCustomDrawArgsReadOnlySpan_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] _DrawCustomDrawArgsReadOnlySpan_GPU_Shaders = [
        new("shaders/instanced.vert.wgsl", vert: "main"),
        new("shaders/vertexPositionColor.frag.wgsl", frag: "main"),
    ];

}