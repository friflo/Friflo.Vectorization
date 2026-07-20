//HintName: VerifyShader/ShaderExample/TexturesTypeMismatch.g.cs
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
    private static partial void TexturesTypeMismatch(
        RenderPass                  pass,
        RenderConfig                config,
        InBuffer<Vector3>           vertices1,
        InBuffer<Vector3>           vertices2,
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
		recorder.Init(_TexturesTypeMismatch_GPU_ShaderId, "TexturesTypeMismatch_encoder"u8);

        recorder.RequireRead     (vertices1);
        recorder.RequireRead     (vertices2);
        
        ref readonly var pipelineCache = ref recorder.Device.GetPipelineCache(_TexturesTypeMismatch_GPU_ShaderId, config, _TexturesTypeMismatch_GPU_WgslHash);
        if (!pipelineCache.IsCreated) {
            pipelineCache = ref _TexturesTypeMismatch_GPU_CreatePipelineCache(recorder.Device, config);
        }
        pass_.SetPipeline(pipelineCache.renderPipeline);
        
        var bindGroupCache = (_TexturesTypeMismatch_GPU_Cache)pipelineCache.bindGroupCache;

        // --- bind group 0
        var key_0 = (texture0.Handle, texture1.Handle, texture2.Handle, texture3.Handle, texture4.Handle, texture5.Handle, texture6.Handle, texture7.Handle, texture8.Handle, texture9.Handle, texture10.Handle, texture11.Handle, texture12.Handle, texture13.Handle, texture14.Handle, texture15.Handle);
        if (!bindGroupCache.bindGroup_0.TryGetValue(key_0, out var bindGroup_0)) {
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
            bindGroup_0 = recorder.CreateBindGroup(pipelineCache.layouts[0], "TexturesTypeMismatch_bindGroup_0"u8);
            bindGroupCache.bindGroup_0.Add(key_0, bindGroup_0);
        }
        pass_.SetBindGroup(0, bindGroup_0);
        
        // --- bind group 1
        var key_1 = (sampler0.Handle, sampler1.Handle);
        if (!bindGroupCache.bindGroup_1.TryGetValue(key_1, out var bindGroup_1)) {
            recorder.BindGroupEntrySampler(0, sampler0);
            recorder.BindGroupEntrySampler(1, sampler1);
            bindGroup_1 = recorder.CreateBindGroup(pipelineCache.layouts[1], "TexturesTypeMismatch_bindGroup_1"u8);
            bindGroupCache.bindGroup_1.Add(key_1, bindGroup_1);
        }
        pass_.SetBindGroup(1, bindGroup_1);
        
        // --- bind group 2
        var key_2 = (vertices1.Handle, vertices2.Handle);
        if (!bindGroupCache.bindGroup_2.TryGetValue(key_2, out var bindGroup_2)) {
            recorder.BindGroupEntryBuffer(0, vertices1.Buffer);
            recorder.BindGroupEntryBuffer(1, vertices2.Buffer);
            bindGroup_2 = recorder.CreateBindGroup(pipelineCache.layouts[2], "TexturesTypeMismatch_bindGroup_2"u8);
            bindGroupCache.bindGroup_2.Add(key_2, bindGroup_2);
        }
        pass_.SetBindGroup(2, bindGroup_2);
        
        // --- draw
    }

    private sealed class _TexturesTypeMismatch_GPU_Cache : BindGroupCache
    {
        internal readonly   Dictionary<(nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint, nint), WgpuBindGroup> bindGroup_0 = new ();
        internal readonly   Dictionary<(nint, nint), WgpuBindGroup> bindGroup_1 = new ();
        internal readonly   Dictionary<(nint, nint), WgpuBindGroup> bindGroup_2 = new ();

        protected override void Clear() {
            ReleaseBindGroups(bindGroup_0);
            ReleaseBindGroups(bindGroup_1);
            ReleaseBindGroups(bindGroup_2);
        }
    }

    private static readonly int _TexturesTypeMismatch_GPU_ShaderId            =  ShaderRegistry.NewShaderId("TexturesTypeMismatch");
    private const  ulong        _TexturesTypeMismatch_GPU_layout_0_Key        =  0x485d5e82576b5a4c;
    private const  ulong        _TexturesTypeMismatch_GPU_layout_1_Key        =  0x2946de09149f7eb0;
    private const  ulong        _TexturesTypeMismatch_GPU_layout_2_Key        =  0x979100e6d4ed93d9;

    private static ulong        _TexturesTypeMismatch_GPU_WgslHash            => 0xdfedd3c4778a619cUL;  // support Hot-Reload

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref readonly PipelineCache _TexturesTypeMismatch_GPU_CreatePipelineCache(WgpuDevice device, RenderConfig config)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[3];
        var layout_0 = device.GetBindGroupLayout(_TexturesTypeMismatch_GPU_layout_0_Key);
        if (!layout_0.IsCreated) {
            device.BindGroupLayoutTexture(0, TextureSampleType.Sint, TextureViewDimension.D1D, false);
            device.BindGroupLayoutTexture(1, TextureSampleType.Uint, TextureViewDimension.D2D, false);
            device.BindGroupLayoutTexture(2, TextureSampleType.Float, TextureViewDimension.D2DArray, false);
            device.BindGroupLayoutTexture(3, TextureSampleType.Uint, TextureViewDimension.D3D, false);
            device.BindGroupLayoutTexture(4, TextureSampleType.Sint, TextureViewDimension.Cube, false);
            device.BindGroupLayoutTexture(5, TextureSampleType.Sint, TextureViewDimension.CubeArray, false);
            device.BindGroupLayoutTexture(6, TextureSampleType.Float, TextureViewDimension.D2D, true);
            device.BindGroupLayoutTexture(7, TextureSampleType.Depth, TextureViewDimension.D2D, true);
            device.BindGroupLayoutStorageTexture(8, TextureFormat.RGBA32Float, StorageTextureAccess.WriteOnly, TextureViewDimension.D1D);
            device.BindGroupLayoutStorageTexture(9, TextureFormat.RGBA8UnormSrgb, StorageTextureAccess.ReadOnly, TextureViewDimension.D2D);
            device.BindGroupLayoutStorageTexture(10, TextureFormat.RGBA8Sint, StorageTextureAccess.WriteOnly, TextureViewDimension.D2DArray);
            device.BindGroupLayoutStorageTexture(11, TextureFormat.R32Uint, StorageTextureAccess.ReadWrite, TextureViewDimension.D3D);
            device.BindGroupLayoutTexture(12, TextureSampleType.Depth, TextureViewDimension.D2D, false);
            device.BindGroupLayoutTexture(13, TextureSampleType.Depth, TextureViewDimension.D2DArray, false);
            device.BindGroupLayoutTexture(14, TextureSampleType.Depth, TextureViewDimension.Cube, false);
            device.BindGroupLayoutTexture(15, TextureSampleType.Depth, TextureViewDimension.CubeArray, false);
            layout_0 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _TexturesTypeMismatch_GPU_layout_0_Key, "TexturesTypeMismatch_layout_0"u8);
        }
        layouts[0] = layout_0;
        
        var layout_1 = device.GetBindGroupLayout(_TexturesTypeMismatch_GPU_layout_1_Key);
        if (!layout_1.IsCreated) {
            device.BindGroupLayoutSampler(0, SamplerBindingType.Filtering);
            device.BindGroupLayoutSampler(1, SamplerBindingType.Comparison);
            layout_1 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _TexturesTypeMismatch_GPU_layout_1_Key, "TexturesTypeMismatch_layout_1"u8);
        }
        layouts[1] = layout_1;
        
        var layout_2 = device.GetBindGroupLayout(_TexturesTypeMismatch_GPU_layout_2_Key);
        if (!layout_2.IsCreated) {
            device.BindGroupLayoutBuffer(0, BufferBindingType.Uniform);
            device.BindGroupLayoutBuffer(1, BufferBindingType.ReadOnlyStorage);
            layout_2 = device.CreateBindGroupLayout(ShaderStage.Vertex | ShaderStage.Fragment, _TexturesTypeMismatch_GPU_layout_2_Key, "TexturesTypeMismatch_layout_2"u8);
        }
        layouts[2] = layout_2;
        
        var pipeline = device.CreateRenderPipeline(layouts, config, typeof(ShaderExample), _TexturesTypeMismatch_GPU_Shaders, "TexturesTypeMismatch_pipeline"u8);

        var bindGroupCache = new _TexturesTypeMismatch_GPU_Cache();
        return ref device.CreatePipelineCache(_TexturesTypeMismatch_GPU_ShaderId, config, _TexturesTypeMismatch_GPU_WgslHash, pipeline, layouts, bindGroupCache);
    }
    
    private static readonly WgpuShader[] _TexturesTypeMismatch_GPU_Shaders = [
        new("shaders/tests/testTextureTypes.frag.wgsl"),
    ];

}