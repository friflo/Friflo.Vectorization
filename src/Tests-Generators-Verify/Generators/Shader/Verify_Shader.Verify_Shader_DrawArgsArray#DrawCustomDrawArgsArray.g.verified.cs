//HintName: VerifyShader/ShaderExample/DrawCustomDrawArgsArray.g.cs
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
    private static partial void DrawCustomDrawArgsArray(
        RenderPass                  pass,
        RenderConfig                config,
        in Uniforms                 uniforms,
        InBuffer<float>             verticesBuffer,
        DrawArgs[]                  customArgs)
    {

        var pass_       = pass.Internal;
        var recorder    = pass_.Recorder;
        recorder.InitShader(_DrawCustomDrawArgsArray_GPU_ShaderId);

        recorder.RequireRead     (verticesBuffer);
        
        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(_DrawCustomDrawArgsArray_GPU_ShaderId, config, _DrawCustomDrawArgsArray_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref _DrawCustomDrawArgsArray_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (_DrawCustomDrawArgsArray_GPU_Cache)pipelineCache.bindGroupCache;

        // --- bind group 0
        pass_.SetBindGroupUniform(0, 0, ref bindGroupCache.bindGroup_0, uniforms, pipelineCache,"DrawCustomDrawArgsArray_bindGroup_0"u8);
        
        pass_.SetVertexBuffer(verticesBuffer, 0);
        
        // --- draw
        foreach(var customArgsItem in customArgs) {
            pass_.Draw(verticesBuffer, 0, config, customArgsItem);
        }
    }

    private sealed class _DrawCustomDrawArgsArray_GPU_Cache : BindGroupCache
    {
        internal            WgpuBindGroup bindGroup_0;

        protected override void Clear() {
            ReleaseBindGroup(ref bindGroup_0);
        }
    }

    private static readonly int _DrawCustomDrawArgsArray_GPU_ShaderId            =  ShaderRegistry.NewShaderId("DrawCustomDrawArgsArray");
    private const  ulong        _DrawCustomDrawArgsArray_GPU_layout_0_Key        =  0xad2eca77479f2364;

    private static ulong        _DrawCustomDrawArgsArray_GPU_WgslHash            => 0x7bea408b45888bf2UL;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache _DrawCustomDrawArgsArray_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[1];
        var layout_0 = device.GetBindGroupLayout(_DrawCustomDrawArgsArray_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutUniform(0);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _DrawCustomDrawArgsArray_GPU_layout_0_Key, "DrawCustomDrawArgsArray_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(ShaderExample), _DrawCustomDrawArgsArray_GPU_Shaders, "DrawCustomDrawArgsArray_pipeline"u8);

        var bindGroupCache = new _DrawCustomDrawArgsArray_GPU_Cache();
        return ref device.CreatePipelineCache(_DrawCustomDrawArgsArray_GPU_ShaderId, config, _DrawCustomDrawArgsArray_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] _DrawCustomDrawArgsArray_GPU_Shaders = [
        new("shaders/instancedCube/instanced.vert.wgsl", vert: "main"),
        new("shaders/vertexPositionColor.frag.wgsl", frag: "main"),
    ];

}