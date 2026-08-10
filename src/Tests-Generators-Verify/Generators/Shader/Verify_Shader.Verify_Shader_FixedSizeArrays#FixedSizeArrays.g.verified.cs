//HintName: VerifyShader/ShaderExample/FixedSizeArrays.g.cs
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
    public static partial void FixedSizeArrays(
        RenderPass                  pass,
        RenderConfig                config,
        in Vector4_UniArr_8         uniform0,
        in DirectUniform2_UniArr_8  uniform1,
        in UniformWithArray         uniform2)
    {

        var pass_       = pass.Internal;
        var recorder    = pass_.Recorder;
        recorder.InitShader(_FixedSizeArrays_GPU_ShaderId);

        
        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(_FixedSizeArrays_GPU_ShaderId, config, _FixedSizeArrays_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref _FixedSizeArrays_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (_FixedSizeArrays_GPU_Cache)pipelineCache.bindGroupCache;

        // --- bind group 0
        if (!bindGroupCache.bindGroup_0.IsCreated) {
            recorder.BindGroupEntryUniform<Vector4_UniArr_8>(0);
            recorder.BindGroupEntryUniform<DirectUniform2_UniArr_8>(1);
            recorder.BindGroupEntryUniform<UniformWithArray>(2);
            bindGroupCache.bindGroup_0 = recorder.CreateBindGroup(pipelineCache.layouts[0], "FixedSizeArrays_bindGroup_0"u8);
        }
        pass_.AddUniform(uniform0);
        pass_.AddUniform(uniform1);
        pass_.AddUniform(uniform2);
        pass_.SetBindGroupUniforms(0, bindGroupCache.bindGroup_0);
        
        // --- draw
    }

    private sealed class _FixedSizeArrays_GPU_Cache : BindGroupCache
    {
        internal            WgpuBindGroup bindGroup_0;

        protected override void Clear() {
            ReleaseBindGroup(ref bindGroup_0);
        }
    }

    private static readonly int _FixedSizeArrays_GPU_ShaderId            =  ShaderRegistry.NewShaderId("FixedSizeArrays");
    private const  ulong        _FixedSizeArrays_GPU_layout_0_Key        =  0x3f735313111c1e87;

    private static ulong        _FixedSizeArrays_GPU_WgslHash            => 0xce509b4928d6832UL;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache _FixedSizeArrays_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[1];
        var layout_0 = device.GetBindGroupLayout(_FixedSizeArrays_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutUniform(0);
            device.BindGroupLayoutUniform(1);
            device.BindGroupLayoutUniform(2);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _FixedSizeArrays_GPU_layout_0_Key, "FixedSizeArrays_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(ShaderExample), _FixedSizeArrays_GPU_Shaders, "FixedSizeArrays_pipeline"u8);

        var bindGroupCache = new _FixedSizeArrays_GPU_Cache();
        return ref device.CreatePipelineCache(_FixedSizeArrays_GPU_ShaderId, config, _FixedSizeArrays_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] _FixedSizeArrays_GPU_Shaders = [
        new("shaders/tests/testTypeSize.wgsl"),
    ];

}