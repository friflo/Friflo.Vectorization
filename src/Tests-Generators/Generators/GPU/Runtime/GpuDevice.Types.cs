// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.ComponentModel;
using Silk.NET.WebGPU;

// file contains structs created by:  GpuDevice

// ReSharper disable InconsistentNaming
namespace Friflo.Vectorization.GPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public unsafe struct GpuEffect 
{
    internal readonly   GpuComputePipeline  pipeline;
    internal readonly   GpuBindGroupLayout  bufferLayout;
    internal readonly   GpuBindGroupLayout  uniformLayout;
    internal            GpuBufferCache      bufferCache;
    public              bool                IsCreated => bufferLayout.handle != null;

    public   override   string              ToString()=> bufferLayout.handle != null ? "Created" : "null";

    internal GpuEffect (GpuComputePipeline pipeline, GpuBindGroupLayout  bufferLayout, GpuBindGroupLayout uniformLayout) {
        this.pipeline       = pipeline;
        this.bufferLayout   = bufferLayout;
        this.uniformLayout  = uniformLayout;
    }
}

public struct GpuBufferCache
{
    private ulong           group0_hash;         
    private ulong           group1_hash;
    private GpuBindGroup    group0;
    private GpuBindGroup    group1;
    private int             lruIndex;
    
    public GpuBindGroup GetGroup(ulong groupHash)
    {
        if (group0_hash == groupHash) return group0;
        if (group1_hash == groupHash) return group1;
        return default;
    }

    internal unsafe void Update(WebGPU wgpu, GpuBindGroup bindGroup, ulong hash)
    {
        lruIndex = lruIndex == 1 ? 0 : 1;
        if (lruIndex == 0) {
            if (group0.handle != null) wgpu.BindGroupRelease(group0.handle);
            group0      = bindGroup;
            group0_hash = hash;
            return;
        }
        if (group1.handle != null) wgpu.BindGroupRelease(group1.handle);
        group1      = bindGroup;
        group1_hash = hash;
    }
    
    internal unsafe void Release(WebGPU wgpu)
    {
        if (group0.handle != null) wgpu.BindGroupRelease(group0.handle);
        if (group1.handle != null) wgpu.BindGroupRelease(group1.handle);
        group0 = default;
        group1 = default;
    }
}


[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct GpuComputePipeline
{
    internal readonly   ComputePipeline*    handle;
    public   override   string              ToString() => handle != null ? "Created" : "null";
    
    internal GpuComputePipeline(ComputePipeline* handle) {
        this.handle = handle;
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct GpuShaderModule
{
    internal readonly   ShaderModule*   handle;
    public   override   string          ToString() => handle != null ? "Created" : "null";
    
    internal GpuShaderModule(ShaderModule* handle) {
        this.handle = handle;
    }
}
