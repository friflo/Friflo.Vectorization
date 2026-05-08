// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.ComponentModel;
using Silk.NET.WebGPU;

// file contains structs created by:  GpuDevice

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public unsafe struct GpuEffect 
{
    public   readonly   GpuComputePipeline  pipeline;
    public   readonly   GpuBindGroupLayout  bufferLayout;
    public   readonly   GpuBindGroupLayout  uniformLayout;
    public              GpuBufferCache      bufferCache;
    public              bool                IsCreated => bufferLayout.handle != null;

    public   override   string              ToString()=> bufferLayout.handle != null ? "Created" : "null";

    internal GpuEffect (GpuComputePipeline pipeline, GpuBindGroupLayout  bufferLayout, GpuBindGroupLayout uniformLayout) {
        this.pipeline       = pipeline;
        this.bufferLayout   = bufferLayout;
        this.uniformLayout  = uniformLayout;
    }
}

internal struct CacheEntry
{
    internal GpuBindGroup   bindGroup;
    internal ulong          hash;
    
    public override string  ToString() => bindGroup.ToString();
    
    internal unsafe void Update(WebGPU wgpu, GpuBindGroup group, ulong groupHash) {
        if (bindGroup.handle != null) wgpu.BindGroupRelease(bindGroup.handle);
        wgpu.BindGroupReference(group.handle);
        bindGroup   = group;
        hash        = groupHash;
    }
    
    internal unsafe void Release(WebGPU wgpu)
    {
        if (bindGroup.handle != null) wgpu.BindGroupRelease(bindGroup.handle);
        bindGroup   = default;
        hash        = 0;
    }
}

/// <summary> The Cache has only two entries to support double buffer use cases </summary> 
public struct GpuBufferCache
{
    private CacheEntry      group0;
    private CacheEntry      group1;
    private int             lruIndex;
    
    public override string  ToString() => $"lru: {(lruIndex == 0 ? ">[0]< [1] " : " [0] >[1]<")}  |  group0: {group0}  |  group1: {group1}";
    
    public GpuBindGroup GetGroup(ulong groupHash)
    {
        if (group0.hash == groupHash) {
            lruIndex = 0; // mark as currently in use
            return group0.bindGroup;
        }
        if (group1.hash == groupHash) {
            lruIndex = 1; // mark as currently in use
            return group1.bindGroup;
        }
        return default;
    }

    internal void Update(WebGPU wgpu, GpuBindGroup bindGroup, ulong hash)
    {
        lruIndex = lruIndex == 1 ? 0 : 1;
        if (lruIndex == 0) {
            group0.Update(wgpu, bindGroup, hash);
        } else {
            group1.Update(wgpu, bindGroup, hash);
        }
    }
    
    internal void Release(WebGPU wgpu) {
        group0.Release(wgpu);
        group1.Release(wgpu);
    }
}

internal struct CachedGroupLayout
{
    internal ulong              hashKey;
    internal GpuBindGroupLayout layout;

    public override string      ToString() => layout.ToString();
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
