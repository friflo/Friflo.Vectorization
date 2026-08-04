//HintName: VerifyShader/ShaderExample/DeformVertices.g.cs
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
    private static partial void DeformVertices(
        PipelineContext             computeContext,
        InOutBuffer<VertexData>     vertices,
        TimeUniform                 uniform)
    {

        var recorder	= (CommandRecorder)computeContext;
		recorder.InitKernel(_DeformVertices_GPU_ShaderId, "DeformVertices_pipeline"u8);

        recorder.RequireReadWrite(vertices);
        
        using var pass_ = recorder.BeginComputePass("DeformVertices"u8);
        
        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(_DeformVertices_GPU_ShaderId, _DeformVertices_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref _DeformVertices_GPU_CreatePipelineCache(recorder.Device);
        }
        pass_.SetPipeline(pipelineCache.computePipeline);
        
        var bindGroupCache = (_DeformVertices_GPU_Cache)pipelineCache.bindGroupCache;

        // --- bind group 0
        var key_0 = vertices.Handle;
        if (!bindGroupCache.bindGroup_0.TryGetValue(key_0, out var bindGroup_0)) {
            recorder.BindGroupEntryBuffer(0, vertices.Buffer);
            bindGroup_0 = recorder.CreateBindGroup(pipelineCache.layouts[0], "DeformVertices_bindGroup_0"u8);
            bindGroupCache.bindGroup_0.Add(key_0, bindGroup_0);
        }
        pass_.SetBindGroup(0, bindGroup_0);
        
        // --- bind group 1
        pass_.SetBindGroupUniform(1, 0, ref bindGroupCache.bindGroup_1, uniform, pipelineCache,"DeformVertices_bindGroup_1"u8);
        
        // --- compute
        pass_.DispatchWorkgroups((vertices.Length + 63) / 64, 1, 1);
    }

    private sealed class _DeformVertices_GPU_Cache : BindGroupCache
    {
        internal readonly   Dictionary<nint, WgpuBindGroup> bindGroup_0 = new ();
        internal            WgpuBindGroup bindGroup_1;

        protected override void Clear() {
            ReleaseBindGroups(bindGroup_0);
            ReleaseBindGroup(ref bindGroup_1);
        }
    }

    private static readonly int _DeformVertices_GPU_ShaderId            =  ShaderRegistry.NewShaderId("DeformVertices");
    private const  ulong        _DeformVertices_GPU_layout_0_Key        =  0x8d1cce904a3cf317;
    private const  ulong        _DeformVertices_GPU_layout_1_Key        =  0x8475539045585a6c;

    private static ulong        _DeformVertices_GPU_WgslHash            => 0xa95cb4969aa1584aUL;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly ComputeCache _DeformVertices_GPU_CreatePipelineCache(WgpuDevice device)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[2];
        var layout_0 = device.GetBindGroupLayout(_DeformVertices_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutBuffer(0, BufferBindingType.Storage);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Compute, _DeformVertices_GPU_layout_0_Key, "DeformVertices_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        var layout_1 = device.GetBindGroupLayout(_DeformVertices_GPU_layout_1_Key);
        if (!layout_1.IsCreated) {
            device.BindGroupLayoutUniform(0);
            layout_1 = device.CreateBindGroupLayout(ShaderStage.Compute, _DeformVertices_GPU_layout_1_Key, "DeformVertices_layout_1"u8);
        }
        layouts[1] = layout_1;
        
        var pipeline = device.CreateComputePipeline(layouts, typeof(ShaderExample), _DeformVertices_GPU_Shaders, "DeformVertices_pipeline"u8);

        var bindGroupCache = new _DeformVertices_GPU_Cache();
        return ref device.CreateComputeCache(_DeformVertices_GPU_ShaderId, _DeformVertices_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] _DeformVertices_GPU_Shaders = [
        new("shaders/renderTest/deform.wgsl", compute: "cs_main"),
    ];

}