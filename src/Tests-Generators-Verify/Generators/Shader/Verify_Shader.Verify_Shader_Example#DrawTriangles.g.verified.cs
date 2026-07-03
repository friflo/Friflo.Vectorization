//HintName: VerifyShader/ShaderExample/DrawTriangles.g.cs
using System;
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
        RenderPass pass,
        RenderConfig config,
        InBuffer<VertexData> triangles,
        MyUniform myUniform)
    {
        var buffers =
        GpuBuffers.Create(triangles, nameof(triangles));

        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(_DrawTriangles_GPU_ShaderId, "DrawTriangles_encoder"u8);
        
        // recorder.RequireRead(vertices); TODO

        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(_DrawTriangles_GPU_ShaderId, config, _DrawTriangles_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref _DrawTriangles_GPU_CreatePipelineCache(recorder.Device, config);
        }
        
        pass_.SetPipeline(pipelineCache.renderPipeline);
    }


    private sealed class _DrawTriangles_GPU_Cache : BindGroupCache
    {
        // internal readonly   Dictionary<(nint,nint), WgpuBindGroup>    bindGroup0 = new ();
        
        protected override void Clear() {
            // ReleaseBindGroups(bindGroup0);
        }
    }

    private static readonly int _DrawTriangles_GPU_ShaderId            =  ShaderRegistry.NewShaderId("_DrawTriangles_GPU");
    private const  ulong        _DrawTriangles_GPU_layout_0_Key        =  0x4755;  // TODO
    private const  ulong        _DrawTriangles_GPU_layout_1_Key        =  0x4755;  // TODO

    private static ulong        _DrawTriangles_GPU_WgslHash            => 0x1255;  // support Hot-Reload            TODO calculate hash

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache _DrawTriangles_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        var layout_0 = device.GetBindGroupLayout(_DrawTriangles_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
        layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _DrawTriangles_GPU_layout_0_Key, "DrawTriangles_layout_0"u8);
        }
        var layout_1 = device.GetBindGroupLayout(_DrawTriangles_GPU_layout_1_Key);
        if (!layout_1.IsCreated) {
        layout_1 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _DrawTriangles_GPU_layout_1_Key, "DrawTriangles_layout_1"u8);
        }

        using var module = device.CreateShaderModule(_DrawTriangles_GPU_Shader(), "DrawTriangles_Shader"u8);

        /*
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[1];
        layouts[0] = layout_0;

        var pipeline = device.CreateRenderPipeline(layouts, config, vsModule, "main"u8, fsModule, "main"u8, "TextureTest_pipeline"u8);

        var bindGroupCache = new _DrawTriangles_GPU_Cache();
        return ref device.CreatePipelineCache(_DrawTriangles_GPU_ShaderId, config, _DrawTriangles_GPU_WgslHash, pipeline, layouts, bindGroupCache); */
        
        throw new  NotImplementedException();
    }
    private static ReadOnlySpan<byte> _DrawTriangles_GPU_Shader() => WgpuResource.GetResource(typeof(ShaderExample), "Tests-Console.shaders/triangle.wgsl");

}