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
using ForeignNamespace;
using Other.Namespace;

namespace VerifyShader;

public partial class ShaderExample
{
    public static partial void DrawTriangles(
        RenderPass                  pass,
        RenderConfig                config,
        InBuffer<VertexData>        triangles,
        in MyUniform                myUniform)
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
        if (!bindGroupCache.bindGroup0.TryGetValue(key_0, out var bindGroup0)) {
            recorder.BindGroupEntryBuffer(triangles.Buffer);
            bindGroup0 = recorder.CreateBindGroup(pipelineCache.layouts[0], "DrawTriangles_bindGroup0"u8);
            bindGroupCache.bindGroup0.Add(key_0, bindGroup0);
        }
        pass_.SetBindGroup(0, bindGroup0);
        
        // --- bind group 1
        pass_.SetBindGroupUniform(1, ref bindGroupCache.bindGroup1, myUniform, pipelineCache,"DrawTriangles_bindGroup1"u8);
        
        // --- draw
        pass_.Draw(triangles.Length, 1, 0, 0);
    }

    private sealed class _DrawTriangles_GPU_Cache : BindGroupCache
    {
        internal readonly   Dictionary<nint, WgpuBindGroup>    bindGroup0 = new ();
        internal            WgpuBindGroup bindGroup1;

        protected override void Clear() {
            ReleaseBindGroups(bindGroup0);
            ReleaseBindGroup(ref bindGroup1);
        }
    }

    private static readonly int _DrawTriangles_GPU_ShaderId            =  ShaderRegistry.NewShaderId("DrawTriangles");
    private const  ulong        _DrawTriangles_GPU_layout_0_Key        =  0x8d19ce904a37da17;
    private const  ulong        _DrawTriangles_GPU_layout_1_Key        =  0x8475539045585a6c;

    private static ulong        _DrawTriangles_GPU_WgslHash            => 0x0UL;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache _DrawTriangles_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[2];
        var layout_0 = device.GetBindGroupLayout(_DrawTriangles_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutBuffer(BufferBindingType.ReadOnlyStorage);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _DrawTriangles_GPU_layout_0_Key, "DrawTriangles_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        var layout_1 = device.GetBindGroupLayout(_DrawTriangles_GPU_layout_1_Key);
        if (!layout_1.IsCreated) {
            device.BindGroupLayoutUniform();
            layout_1 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _DrawTriangles_GPU_layout_1_Key, "DrawTriangles_layout_1"u8);
        }
        layouts[1] = layout_1;
        
        using var module = device.CreateShaderModule(_DrawTriangles_GPU_Shader(), "DrawTriangles_Shader"u8);

        var pipeline = device.CreateRenderPipeline(layouts, config, module, "vs_main"u8, module, "fs_main"u8, "DrawTriangles_pipeline"u8);

        var bindGroupCache = new _DrawTriangles_GPU_Cache();
        return ref device.CreatePipelineCache(_DrawTriangles_GPU_ShaderId, config, _DrawTriangles_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static ReadOnlySpan<byte> _DrawTriangles_GPU_Shader() => WgpuResource.GetResource(typeof(ShaderExample), "shaders/triangle.wgsl");

}