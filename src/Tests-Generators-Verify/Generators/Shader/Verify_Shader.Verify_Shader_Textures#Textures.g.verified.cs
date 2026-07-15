//HintName: VerifyShader/ShaderExample/Textures.g.cs
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
    private static partial void Textures(
        RenderPass                  pass,
        RenderConfig                config,
        GpuTextureView              texture0,
        GpuTextureView              texture1,
        GpuTextureView              texture2,
        GpuTextureView              texture3,
        GpuTextureView              texture4,
        GpuTextureView              texture5,
        GpuTextureView              texture6,
        GpuTextureView              texture7,
        GpuTextureView              texture8,
        GpuTextureView              texture9,
        GpuTextureView              texture10,
        GpuTextureView              texture11,
        GpuTextureView              texture12,
        GpuTextureView              texture13,
        GpuTextureView              texture14,
        GpuTextureView              texture15,
        GpuSampler                  sampler0,
        GpuSampler                  sampler1)
    {

        var pass_       = pass.Internal;
		var recorder	= pass_.Recorder;
		recorder.Init(_Textures_GPU_ShaderId, "Textures_encoder"u8);

        
        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(_Textures_GPU_ShaderId, config, _Textures_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref _Textures_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (_Textures_GPU_Cache)pipelineCache.bindGroupCache;

        // --- bind group 0
        var key_0 = (texture0.Handle, texture1.Handle, texture2.Handle, texture3.Handle, texture4.Handle, texture5.Handle, texture6.Handle, texture7.Handle, texture8.Handle, texture9.Handle, texture10.Handle, texture11.Handle, texture12.Handle, texture13.Handle, texture14.Handle, texture15.Handle);
        if (!bindGroupCache.bindGroup0.TryGetValue(key_0, out var bindGroup0)) {
            recorder.BindGroupEntryTexture(0, texture0);
            recorder.BindGroupEntryTexture(1, texture1);
            recorder.BindGroupEntryTexture(2, texture2);
            recorder.BindGroupEntryTexture(3, texture3);
            recorder.BindGroupEntryTexture(4, texture4);
            recorder.BindGroupEntryTexture(5, texture5);
            recorder.BindGroupEntryTexture(6, texture6);
            recorder.BindGroupEntryTexture(7, texture7);
            recorder.BindGroupEntryTexture(8, texture8);
            recorder.BindGroupEntryTexture(9, texture9);
            recorder.BindGroupEntryTexture(10, texture10);
            recorder.BindGroupEntryTexture(11, texture11);
            recorder.BindGroupEntryTexture(12, texture12);
            recorder.BindGroupEntryTexture(13, texture13);
            recorder.BindGroupEntryTexture(14, texture14);
            recorder.BindGroupEntryTexture(15, texture15);
            bindGroup0 = recorder.CreateBindGroup(pipelineCache.layouts[0], "Textures_bindGroup0"u8);
            bindGroupCache.bindGroup0.Add(key_0, bindGroup0);
        }
        pass_.SetBindGroup(0, bindGroup0);
        
        // --- bind group 1
        var key_1 = (sampler0.Handle, sampler1.Handle);
        if (!bindGroupCache.bindGroup1.TryGetValue(key_1, out var bindGroup1)) {
            recorder.BindGroupEntrySampler(0, sampler0);
            recorder.BindGroupEntrySampler(1, sampler1);
            bindGroup1 = recorder.CreateBindGroup(pipelineCache.layouts[1], "Textures_bindGroup1"u8);
            bindGroupCache.bindGroup1.Add(key_1, bindGroup1);
        }
        pass_.SetBindGroup(1, bindGroup1);
        
        // --- draw
    }

    private sealed class _Textures_GPU_Cache : BindGroupCache
    {
        internal readonly   Dictionary<(nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint), WgpuBindGroup>    bindGroup0 = new ();
        internal readonly   Dictionary<(nint, nint), WgpuBindGroup>    bindGroup1 = new ();

        protected override void Clear() {
            ReleaseBindGroups(bindGroup0);
            ReleaseBindGroups(bindGroup1);
        }
    }

    private static readonly int _Textures_GPU_ShaderId            =  ShaderRegistry.NewShaderId("Textures");
    private const  ulong        _Textures_GPU_layout_0_Key        =  0xb1244d2c118c100c;
    private const  ulong        _Textures_GPU_layout_1_Key        =  0x1c264884083a5675;

    private static ulong        _Textures_GPU_WgslHash            => 0x6daa93cb8c2d50ccUL;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache _Textures_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[2];
        var layout_0 = device.GetBindGroupLayout(_Textures_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutTexture(0, TextureSampleType.Float, TextureViewDimension.D1D, false);
            device.BindGroupLayoutTexture(1, TextureSampleType.Float, TextureViewDimension.D2D, false);
            device.BindGroupLayoutTexture(2, TextureSampleType.Sint, TextureViewDimension.D2DArray, false);
            device.BindGroupLayoutTexture(3, TextureSampleType.Sint, TextureViewDimension.D3D, false);
            device.BindGroupLayoutTexture(4, TextureSampleType.Uint, TextureViewDimension.Cube, false);
            device.BindGroupLayoutTexture(5, TextureSampleType.Uint, TextureViewDimension.CubeArray, false);
            device.BindGroupLayoutTexture(6, TextureSampleType.Sint, TextureViewDimension.D2D, true);
            device.BindGroupLayoutTexture(7, TextureSampleType.Depth, TextureViewDimension.D2D, true);
            device.BindGroupLayoutStorageTexture(8, TextureFormat.RGBA32Float, StorageTextureAccess.WriteOnly, TextureViewDimension.D1D);
            device.BindGroupLayoutStorageTexture(9, TextureFormat.RGBA8Unorm, StorageTextureAccess.WriteOnly, TextureViewDimension.D2D);
            device.BindGroupLayoutStorageTexture(10, TextureFormat.RGBA8Uint, StorageTextureAccess.WriteOnly, TextureViewDimension.D2DArray);
            device.BindGroupLayoutStorageTexture(11, TextureFormat.R32Float, StorageTextureAccess.WriteOnly, TextureViewDimension.D3D);
            device.BindGroupLayoutTexture(12, TextureSampleType.Depth, TextureViewDimension.D2D, false);
            device.BindGroupLayoutTexture(13, TextureSampleType.Depth, TextureViewDimension.D2DArray, false);
            device.BindGroupLayoutTexture(14, TextureSampleType.Depth, TextureViewDimension.Cube, false);
            device.BindGroupLayoutTexture(15, TextureSampleType.Depth, TextureViewDimension.CubeArray, false);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _Textures_GPU_layout_0_Key, "Textures_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        var layout_1 = device.GetBindGroupLayout(_Textures_GPU_layout_1_Key);
        if (!layout_1.IsCreated) {
            device.BindGroupLayoutSampler(0, SamplerBindingType.Filtering);
            device.BindGroupLayoutSampler(1, SamplerBindingType.Comparison);
            layout_1 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _Textures_GPU_layout_1_Key, "Textures_layout_1"u8);
        }
        layouts[1] = layout_1;
        
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(ShaderExample), _Textures_GPU_Shaders, "Textures_pipeline"u8);

        var bindGroupCache = new _Textures_GPU_Cache();
        return ref device.CreatePipelineCache(_Textures_GPU_ShaderId, config, _Textures_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] _Textures_GPU_Shaders = [
        new WgpuShader("shaders/testTextureTypes.frag.wgsl"),
    ];

}