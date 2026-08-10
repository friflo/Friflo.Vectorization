//HintName: VerifyShader/ShaderExample/ConcreteTypes.g.cs
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
    public static partial void ConcreteTypes(
        RenderPass                  pass,
        RenderConfig                config,
        in Point4                   uniform0,
        in Point3                   uniform1,
        in Vector3                  uniform2)
    {

        var pass_       = pass.Internal;
        var recorder    = pass_.Recorder;
        recorder.InitShader(_ConcreteTypes_GPU_ShaderId);

        
        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(_ConcreteTypes_GPU_ShaderId, config, _ConcreteTypes_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref _ConcreteTypes_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (_ConcreteTypes_GPU_Cache)pipelineCache.bindGroupCache;

        // --- bind group 0
        if (!bindGroupCache.bindGroup_0.IsCreated) {
            recorder.BindGroupEntryUniform<Point4>(0);
            recorder.BindGroupEntryUniform<Point3>(1);
            recorder.BindGroupEntryUniform<Vector3>(2);
            bindGroupCache.bindGroup_0 = recorder.CreateBindGroup(pipelineCache.layouts[0], "ConcreteTypes_bindGroup_0"u8);
        }
        pass_.AddUniform(uniform0);
        pass_.AddUniform(uniform1);
        pass_.AddUniform(uniform2);
        pass_.SetBindGroupUniforms(0, bindGroupCache.bindGroup_0);
        
        // --- draw
    }

    private sealed class _ConcreteTypes_GPU_Cache : BindGroupCache
    {
        internal            WgpuBindGroup bindGroup_0;

        protected override void Clear() {
            ReleaseBindGroup(ref bindGroup_0);
        }
    }

    private static readonly int _ConcreteTypes_GPU_ShaderId            =  ShaderRegistry.NewShaderId("ConcreteTypes");
    private const  ulong        _ConcreteTypes_GPU_layout_0_Key        =  0x3f735313111c1e87;

    private static ulong        _ConcreteTypes_GPU_WgslHash            => 0xdfb06915e0648acbUL;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache _ConcreteTypes_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[1];
        var layout_0 = device.GetBindGroupLayout(_ConcreteTypes_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutUniform(0);
            device.BindGroupLayoutUniform(1);
            device.BindGroupLayoutUniform(2);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _ConcreteTypes_GPU_layout_0_Key, "ConcreteTypes_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(ShaderExample), _ConcreteTypes_GPU_Shaders, "ConcreteTypes_pipeline"u8);

        var bindGroupCache = new _ConcreteTypes_GPU_Cache();
        return ref device.CreatePipelineCache(_ConcreteTypes_GPU_ShaderId, config, _ConcreteTypes_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] _ConcreteTypes_GPU_Shaders = [
        new("shaders/tests/testTypeSize2.wgsl"),
    ];

}