//HintName: VerifyShader/ShaderExample/DrawTriangles.g.cs
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
    public static partial void DrawTriangles(
        RenderPass                  pass,
        RenderConfig                config,
        InBuffer<VertexData>        triangles,
        in MyUniform                myUniform,
        Vector2                     model_offset)
    {

        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(_DrawTriangles_GPU_ShaderId, "DrawTriangles_encoder"u8);

        recorder.RequireRead     (triangles);
        
        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(_DrawTriangles_GPU_ShaderId, config, _DrawTriangles_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref _DrawTriangles_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (_DrawTriangles_GPU_Cache)pipelineCache.bindGroupCache;

        // --- bind group 0
        var key_0 = triangles.Handle;
        if (!bindGroupCache.bindGroup_0.TryGetValue(key_0, out var bindGroup_0)) {
            recorder.BindGroupEntryBuffer(0, triangles.Buffer);
            bindGroup_0 = recorder.CreateBindGroup(pipelineCache.layouts[0], "DrawTriangles_bindGroup_0"u8);
            bindGroupCache.bindGroup_0.Add(key_0, bindGroup_0);
        }
        pass_.SetBindGroup(0, bindGroup_0);
        
        // --- bind group 2
        if (!bindGroupCache.bindGroup_2.IsCreated) {
            recorder.BindGroupEntryUniform<MyUniform>(0);
            recorder.BindGroupEntryUniform<Vector2>(1);
            bindGroupCache.bindGroup_2 = recorder.CreateBindGroup(pipelineCache.layouts[2], "DrawTriangles_bindGroup_2"u8);
        }
        pass_.AddUniform(myUniform);
        pass_.AddUniform(model_offset);
        pass_.SetBindGroupUniforms(2, bindGroupCache.bindGroup_2);
        
        // --- draw
        pass_.Draw(triangles, new DrawArgs());
    }

    private sealed class _DrawTriangles_GPU_Cache : BindGroupCache
    {
        internal readonly   Dictionary<nint, WgpuBindGroup>    bindGroup_0 = new ();
        internal            WgpuBindGroup bindGroup_2;

        protected override void Clear() {
            ReleaseBindGroups(bindGroup_0);
            ReleaseBindGroup(ref bindGroup_2);
        }
    }

    private static readonly int _DrawTriangles_GPU_ShaderId            =  ShaderRegistry.NewShaderId("DrawTriangles");
    private const  ulong        _DrawTriangles_GPU_layout_0_Key        =  0xed212287f4058386;
    private const  ulong        _DrawTriangles_GPU_layout_2_Key        =  0x411100ebbcf4bdd9;

    private static ulong        _DrawTriangles_GPU_WgslHash            => 0x259828d805e43104UL;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache _DrawTriangles_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[3];
        var layout_0 = device.GetBindGroupLayout(_DrawTriangles_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutBuffer(0, BufferBindingType.ReadOnlyStorage);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _DrawTriangles_GPU_layout_0_Key, "DrawTriangles_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        layouts[1] = device.GetEmptyBindGroupLayout();
        
        var layout_2 = device.GetBindGroupLayout(_DrawTriangles_GPU_layout_2_Key);
        if (!layout_2.IsCreated) {
            device.BindGroupLayoutUniform(0);
            device.BindGroupLayoutUniform(1);
            layout_2 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _DrawTriangles_GPU_layout_2_Key, "DrawTriangles_layout_2"u8);
        }
        layouts[2] = layout_2;
        
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(ShaderExample), _DrawTriangles_GPU_Shaders, "DrawTriangles_pipeline"u8);

        var bindGroupCache = new _DrawTriangles_GPU_Cache();
        return ref device.CreatePipelineCache(_DrawTriangles_GPU_ShaderId, config, _DrawTriangles_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] _DrawTriangles_GPU_Shaders = [
        new("shaders/triangle.wgsl", vert: "vs_main", frag: "fs_main"),
    ];

}