//HintName: VerifyShader/ShaderExample/DrawInstanced.g.cs
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
    private static partial void DrawInstanced(
        RenderPass                  pass,
        RenderConfig                config,
        InBuffer<float>             verticesBuffer,
        InBuffer<Matrix4x4>         mvpMatrices)
    {

        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(_DrawInstanced_GPU_ShaderId, "DrawInstanced_encoder"u8);

        recorder.RequireRead     (verticesBuffer);
        recorder.RequireRead     (mvpMatrices);
        
        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(_DrawInstanced_GPU_ShaderId, config, _DrawInstanced_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref _DrawInstanced_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (_DrawInstanced_GPU_Cache)pipelineCache.bindGroupCache;

        // --- bind group 0
        var key_0 = mvpMatrices.Handle;
        if (!bindGroupCache.bindGroup0.TryGetValue(key_0, out var bindGroup0)) {
            recorder.BindGroupEntryBuffer(mvpMatrices.Buffer);
            bindGroup0 = recorder.CreateBindGroup(pipelineCache.layouts[0], "DrawInstanced_bindGroup0"u8);
            bindGroupCache.bindGroup0.Add(key_0, bindGroup0);
        }
        pass_.SetBindGroup(0, bindGroup0);
        
        pass_.SetVertexBuffer(verticesBuffer, 0);
        
        // --- draw
        pass_.Draw(verticesBuffer, 0, config, mvpMatrices.Length, 0, 0);
    }

    private sealed class _DrawInstanced_GPU_Cache : BindGroupCache
    {
        internal readonly   Dictionary<nint, WgpuBindGroup>    bindGroup0 = new ();

        protected override void Clear() {
            ReleaseBindGroups(bindGroup0);
        }
    }

    private static readonly int _DrawInstanced_GPU_ShaderId            =  ShaderRegistry.NewShaderId("DrawInstanced");
    private const  ulong        _DrawInstanced_GPU_layout_0_Key        =  0xad2bca77479a0a64;

    private static ulong        _DrawInstanced_GPU_WgslHash            => 0x0UL;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache _DrawInstanced_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[1];
        var layout_0 = device.GetBindGroupLayout(_DrawInstanced_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutBuffer(BufferBindingType.Uniform);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _DrawInstanced_GPU_layout_0_Key, "DrawInstanced_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(ShaderExample), _DrawInstanced_GPU_Shaders(), "DrawInstanced_pipeline"u8);

        var bindGroupCache = new _DrawInstanced_GPU_Cache();
        return ref device.CreatePipelineCache(_DrawInstanced_GPU_ShaderId, config, _DrawInstanced_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static WgpuShader[] _DrawInstanced_GPU_Shaders() => [
        new WgpuShader("shaders/instanced.vert.wgsl", vert: "main"),
        new WgpuShader("shaders/vertexPositionColor.frag.wgsl", frag: "main"),
    ];

}