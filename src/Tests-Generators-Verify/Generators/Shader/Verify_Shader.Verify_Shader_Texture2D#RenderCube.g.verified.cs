//HintName: VerifyShader/ShaderExample/RenderCube.g.cs
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
    public static partial void RenderCube(
        RenderPass pass,
        RenderConfig config,
        InBuffer<Single> vertices,
        Uniforms uniforms,
        GpuSampler smoothFilter,
        GpuTextureView material)
    {

        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(_RenderCube_GPU_ShaderId, "RenderCube_encoder"u8);
        
        // recorder.RequireRead(vertices); TODO

        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(_RenderCube_GPU_ShaderId, config, _RenderCube_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref _RenderCube_GPU_CreatePipelineCache(recorder.Device, config);
        }
        
        pass_.SetPipeline(pipelineCache.renderPipeline);
    }


    private sealed class _RenderCube_GPU_Cache : BindGroupCache
    {
        // internal readonly   Dictionary<(nint,nint), WgpuBindGroup>    bindGroup0 = new ();
        
        protected override void Clear() {
            // ReleaseBindGroups(bindGroup0);
        }
    }

    private static readonly int _RenderCube_GPU_ShaderId            =  ShaderRegistry.NewShaderId("TextureTestShader");
    private const  ulong        _RenderCube_GPU_layout_0_Key        =  0x4755;  // unique key set by Generator   TODO calculate key
    private static ulong        _RenderCube_GPU_WgslHash            => 0x1255;  // support Hot-Reload            TODO calculate hash

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache _RenderCube_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
    /*  var layout_0 = device.GetBindGroupLayout(_RenderCube_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutUniform();
            device.BindGroupLayoutSampler(SamplerBindingType.Filtering);
            device.BindGroupLayoutTexture(TextureSampleType.Float, TextureViewDimension.D2D, false);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _RenderCube_GPU_layout_0_Key, "TextureTest_layout_0"u8);
        }
        using var vsModule = device.CreateShaderModule(_RenderCube_GPU_VertexShader(),   "TextureTest_VertexShader"u8);
        using var fsModule = device.CreateShaderModule(_RenderCube_GPU_FragmentShader(), "TextureTest_FragmentShader"u8);
        
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[1];
        layouts[0] = layout_0;

        var pipeline = device.CreateRenderPipeline(layouts, config, vsModule, "main"u8, fsModule, "main"u8, "TextureTest_pipeline"u8);

        var bindGroupCache = new _RenderCube_GPU_Cache();
        return ref device.CreatePipelineCache(_RenderCube_GPU_ShaderId, config, _RenderCube_GPU_WgslHash, pipeline, layouts, bindGroupCache); */
        
        using var vsModule = device.CreateShaderModule(_RenderCube_GPU_VertexShader(),   "RenderCube_VertexShader"u8);
        using var fsModule = device.CreateShaderModule(_RenderCube_GPU_FragmentShader(), "RenderCube_FragmentShader"u8);

        throw new  NotImplementedException();
    }
    private static ReadOnlySpan<byte> _RenderCube_GPU_VertexShader()   => WgpuResource.GetResource(typeof(ShaderExample), "Tests-Console.shaders/basic.vert.wgsl");
    private static ReadOnlySpan<byte> _RenderCube_GPU_FragmentShader() => WgpuResource.GetResource(typeof(ShaderExample), "Tests-Console.shaders/sampleTextureMixColor.frag.wgsl");

}