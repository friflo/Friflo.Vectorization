//HintName: VerifyShader/ShaderExample/DrawIndexedIndirect.g.cs
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
    private static partial void DrawIndexedIndirect(
        RenderPass                  pass,
        RenderConfig                config,
        in Scene                    scene,
        in Model                    model,
        InBuffer<IndexedIndirect>   indirectBuffer,
        InBuffer<Vector3>           verticesBuffer,
        InBuffer<ushort>            indexBuffer,
        DrawIndirectArgs            args)
    {

        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(_DrawIndexedIndirect_GPU_ShaderId, "DrawIndexedIndirect_encoder"u8);

        recorder.RequireRead     (indirectBuffer);
        recorder.RequireRead     (verticesBuffer);
        recorder.RequireRead     (indexBuffer);
        
        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(_DrawIndexedIndirect_GPU_ShaderId, config, _DrawIndexedIndirect_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref _DrawIndexedIndirect_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (_DrawIndexedIndirect_GPU_Cache)pipelineCache.bindGroupCache;

        // --- bind group 0
        pass_.SetBindGroupUniform(0, ref bindGroupCache.bindGroup0, scene, pipelineCache,"DrawIndexedIndirect_bindGroup0"u8);
        
        // --- bind group 1
        var key_1 = indirectBuffer.Handle;
        if (!bindGroupCache.bindGroup1.TryGetValue(key_1, out var bindGroup1)) {
            recorder.BindGroupEntryUniform<Model>(0);
            recorder.BindGroupEntryBuffer(1, indirectBuffer.Buffer);
            bindGroup1 = recorder.CreateBindGroup(pipelineCache.layouts[1], "DrawIndexedIndirect_bindGroup1"u8);
            bindGroupCache.bindGroup1.Add(key_1, bindGroup1);
        }
        pass_.AddUniform(model);
        pass_.SetBindGroupUniforms(1, bindGroup1);
        
        pass_.SetVertexBuffer(verticesBuffer, 0);
        pass_.SetIndexBuffer(indexBuffer, IndexFormat.Uint16);
        
        // --- draw
        pass_.DrawIndexedIndirect(indirectBuffer, args);
    }

    private sealed class _DrawIndexedIndirect_GPU_Cache : BindGroupCache
    {
        internal            WgpuBindGroup bindGroup0;
        internal readonly   Dictionary<nint, WgpuBindGroup>    bindGroup1 = new ();

        protected override void Clear() {
            ReleaseBindGroup(ref bindGroup0);
            ReleaseBindGroups(bindGroup1);
        }
    }

    private static readonly int _DrawIndexedIndirect_GPU_ShaderId            =  ShaderRegistry.NewShaderId("DrawIndexedIndirect");
    private const  ulong        _DrawIndexedIndirect_GPU_layout_0_Key        =  0x8d16ce904a32c117;
    private const  ulong        _DrawIndexedIndirect_GPU_layout_1_Key        =  0x2a514849282d6f75;

    private static ulong        _DrawIndexedIndirect_GPU_WgslHash            => 0xd0d6ec6e199e95cfUL;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache _DrawIndexedIndirect_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[2];
        var layout_0 = device.GetBindGroupLayout(_DrawIndexedIndirect_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutUniform(0);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _DrawIndexedIndirect_GPU_layout_0_Key, "DrawIndexedIndirect_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        var layout_1 = device.GetBindGroupLayout(_DrawIndexedIndirect_GPU_layout_1_Key);
        if (!layout_1.IsCreated) {
            device.BindGroupLayoutUniform(0);
            device.BindGroupLayoutBuffer(1, BufferBindingType.ReadOnlyStorage);
            layout_1 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _DrawIndexedIndirect_GPU_layout_1_Key, "DrawIndexedIndirect_layout_1"u8);
        }
        layouts[1] = layout_1;
        
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(ShaderExample), _DrawIndexedIndirect_GPU_Shaders, "DrawIndexedIndirect_pipeline"u8);

        var bindGroupCache = new _DrawIndexedIndirect_GPU_Cache();
        return ref device.CreatePipelineCache(_DrawIndexedIndirect_GPU_ShaderId, config, _DrawIndexedIndirect_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] _DrawIndexedIndirect_GPU_Shaders = [
        new WgpuShader("shaders/shadowMapping/vertexShadow.wgsl", vert: "main"),
    ];

}