// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.ComponentModel;
using Silk.NET.WebGPU;
using Webgpu = Silk.NET.WebGPU.WebGPU;

// file contains structs created by:  SilkDevice

// ReSharper disable once CheckNamespace
namespace Kernel.SilkWebGPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public unsafe struct SilkEffect 
{
    public   readonly   SilkComputePipeline pipeline;
    public   readonly   SilkBindGroupLayout bufferLayout;
    public   readonly   SilkBindGroupLayout uniformLayout;
    public              SilkBufferCache     bufferCache;
    public              bool                IsCreated => bufferLayout.handle != null;

    public   override   string              ToString()=> bufferLayout.handle != null ? "Created" : "null";

    internal SilkEffect (SilkComputePipeline pipeline, SilkBindGroupLayout  bufferLayout, SilkBindGroupLayout uniformLayout) {
        this.pipeline       = pipeline;
        this.bufferLayout   = bufferLayout;
        this.uniformLayout  = uniformLayout;
    }
}

internal struct CacheEntry
{
    internal SilkBindGroup  bindGroup;
    internal ulong          hash;
    
    public override string  ToString() => bindGroup.ToString();
    
    internal unsafe void Update(Webgpu wgpu, SilkBindGroup group, ulong groupHash) {
        if (bindGroup.handle != null) wgpu.BindGroupRelease(bindGroup.handle);
        wgpu.BindGroupReference(group.handle);
        bindGroup   = group;
        hash        = groupHash;
    }
    
    internal unsafe void Release(Webgpu wgpu)
    {
        if (bindGroup.handle != null) wgpu.BindGroupRelease(bindGroup.handle);
        bindGroup   = default;
        hash        = 0;
    }
}

/// <summary> The Cache has only two entries to support double buffer use cases </summary>
[EditorBrowsable(EditorBrowsableState.Never)] 
public struct SilkBufferCache
{
    private CacheEntry      group0;
    private CacheEntry      group1;
    private int             lruIndex;
    
    public override string  ToString() => $"lru: {(lruIndex == 0 ? ">[0]< [1] " : " [0] >[1]<")}  |  group0: {group0}  |  group1: {group1}";
    
    public SilkBindGroup GetGroup(ulong groupHash)
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

    internal void Update(Webgpu wgpu, SilkBindGroup bindGroup, ulong hash)
    {
        lruIndex = lruIndex == 1 ? 0 : 1;
        if (lruIndex == 0) {
            group0.Update(wgpu, bindGroup, hash);
        } else {
            group1.Update(wgpu, bindGroup, hash);
        }
    }
    
    internal void Release(Webgpu wgpu) {
        group0.Release(wgpu);
        group1.Release(wgpu);
    }
}

internal struct CachedGroupLayout
{
    internal ulong              	hashKey;
    internal SilkBindGroupLayout 	layout;

    public override string      	ToString() => layout.ToString();
}


[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct SilkComputePipeline
{
    internal readonly   ComputePipeline*    handle;
    public   override   string              ToString() => handle != null ? "Created" : "null";
    
    internal SilkComputePipeline(ComputePipeline* handle) {
        this.handle = handle;
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct SilkShaderModule
{
    internal readonly   ShaderModule*   handle;
    public   override   string          ToString() => handle != null ? "Created" : "null";
    
    internal SilkShaderModule(ShaderModule* handle) {
        this.handle = handle;
    }
}
