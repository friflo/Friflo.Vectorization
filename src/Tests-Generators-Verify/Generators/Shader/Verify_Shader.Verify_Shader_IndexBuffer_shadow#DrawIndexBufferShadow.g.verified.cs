//HintName: VerifyShader/ShaderExample/DrawIndexBufferShadow.g.cs
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
    private static partial void DrawIndexBufferShadow(
        RenderPass                  pass,
        RenderConfig                config,
        in Scene                    scene,
        in Model                    model,
        InBuffer<Vector3>           verticesBuffer,
        InBuffer<ushort>            indexBuffer)
    {

        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(_DrawIndexBufferShadow_GPU_ShaderId, "DrawIndexBufferShadow_encoder"u8);

        recorder.RequireRead     (verticesBuffer);
        recorder.RequireRead     (indexBuffer);
        
        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(_DrawIndexBufferShadow_GPU_ShaderId, config, _DrawIndexBufferShadow_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref _DrawIndexBufferShadow_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (_DrawIndexBufferShadow_GPU_Cache)pipelineCache.bindGroupCache;

        // --- bind group 0
        pass_.SetBindGroupUniform(0, ref bindGroupCache.bindGroup0, scene, pipelineCache,"DrawIndexBufferShadow_bindGroup0"u8);
        
        // --- bind group 1
        pass_.SetBindGroupUniform(1, ref bindGroupCache.bindGroup1, model, pipelineCache,"DrawIndexBufferShadow_bindGroup1"u8);
        
        pass_.SetVertexBuffer(verticesBuffer, 0);
        pass_.SetIndexBuffer(indexBuffer, IndexFormat.Uint16);
        
        // --- draw
        pass_.DrawIndexed(indexBuffer, new DrawArgs());
    }

    private sealed class _DrawIndexBufferShadow_GPU_Cache : BindGroupCache
    {
        internal            WgpuBindGroup bindGroup0;
        internal            WgpuBindGroup bindGroup1;

        protected override void Clear() {
            ReleaseBindGroup(ref bindGroup0);
            ReleaseBindGroup(ref bindGroup1);
        }
    }

    private static readonly int _DrawIndexBufferShadow_GPU_ShaderId            =  ShaderRegistry.NewShaderId("DrawIndexBufferShadow");
    private const  ulong        _DrawIndexBufferShadow_GPU_layout_0_Key        =  0x8d16ce904a32c117;
    private const  ulong        _DrawIndexBufferShadow_GPU_layout_1_Key        =  0x8475539045585a6c;

    private static ulong        _DrawIndexBufferShadow_GPU_WgslHash            => 0xd0d6ec6e199e95cfUL;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache _DrawIndexBufferShadow_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[2];
        var layout_0 = device.GetBindGroupLayout(_DrawIndexBufferShadow_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutUniform();
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _DrawIndexBufferShadow_GPU_layout_0_Key, "DrawIndexBufferShadow_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        var layout_1 = device.GetBindGroupLayout(_DrawIndexBufferShadow_GPU_layout_1_Key);
        if (!layout_1.IsCreated) {
            device.BindGroupLayoutUniform();
            layout_1 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _DrawIndexBufferShadow_GPU_layout_1_Key, "DrawIndexBufferShadow_layout_1"u8);
        }
        layouts[1] = layout_1;
        
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(ShaderExample), _DrawIndexBufferShadow_GPU_Shaders, "DrawIndexBufferShadow_pipeline"u8);

        var bindGroupCache = new _DrawIndexBufferShadow_GPU_Cache();
        return ref device.CreatePipelineCache(_DrawIndexBufferShadow_GPU_ShaderId, config, _DrawIndexBufferShadow_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] _DrawIndexBufferShadow_GPU_Shaders = [
        new WgpuShader("shaders/shadowMapping/vertexShadow.wgsl", vert: "main"),
    ];

}