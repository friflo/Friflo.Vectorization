// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.ComponentModel;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// file contains structs created by:  WgpuDevice

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;

public readonly struct UniformLayout(uint index, WgpuBindGroupLayout layout)
{
    internal readonly   uint                index = index;
    internal readonly   WgpuBindGroupLayout layout = layout;
}

[EditorBrowsable(EditorBrowsableState.Never)]
public unsafe struct WgpuComputeEffect 
{
    public   readonly   WgpuComputePipeline pipeline;
    public   readonly   WgpuBindGroupLayout bufferLayout;
    public   readonly   UniformLayout       uniformLayout;
    public              WgpuBufferCache     computeBufferCache;     // todo rename -> bufferCache
    internal readonly   ulong               wgslHash;
    public              bool                IsCreated => bufferLayout.handle != null;

    public   override   string              ToString()=> bufferLayout.handle != null ? "Created" : "null";

    internal WgpuComputeEffect (int kernelId, ulong wgslHash, WgpuComputePipeline pipeline,
        WgpuBindGroupLayout  bufferLayout, WgpuBindGroupLayout uniformLayout)
    {
        this.wgslHash       = wgslHash;
        this.pipeline       = pipeline;
        this.bufferLayout   = bufferLayout;
        this.uniformLayout  = new UniformLayout((uint)kernelId, uniformLayout);
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public unsafe struct WgpuShaderEffect
{
    public   readonly   WgpuRenderPipeline  renderPipeline;
    public   readonly   WgpuBindGroupLayout bufferLayout;
    public   readonly   UniformLayout       uniformLayout;
    public              WgpuBufferCache     bufferCache;
    internal readonly   ulong               wgslHash;
    public              bool                IsCreated => renderPipeline.handle != null;

    public   override   string              ToString()=> renderPipeline.handle != null ? "Created" : "null";

    internal WgpuShaderEffect (int kernelId, ulong wgslHash, WgpuRenderPipeline renderPipeline,
        WgpuBindGroupLayout  bufferLayout, WgpuBindGroupLayout uniformLayout)
    {
        this.wgslHash       = wgslHash;
        this.renderPipeline = renderPipeline;
        this.bufferLayout   = bufferLayout;
        this.uniformLayout  = new UniformLayout((uint)kernelId, uniformLayout);
    }
}

internal struct CacheEntry
{
    internal WgpuBindGroup  bindGroup;
    internal ulong          hash;
    
    public override string  ToString() => bindGroup.ToString();
    
    internal unsafe void Update(WgpuBindGroup group, ulong groupHash) {
        if (bindGroup.handle != null) wgpuBindGroupRelease(bindGroup.handle);
        wgpuBindGroupAddRef(group.handle);
        bindGroup   = group;
        hash        = groupHash;
    }
    
    internal unsafe void Release()
    {
        if (bindGroup.handle != null) wgpuBindGroupRelease(bindGroup.handle);
        bindGroup   = default;
        hash        = 0;
    }
}

/// <summary> The Cache has only two entries to support double buffer use cases </summary>
[EditorBrowsable(EditorBrowsableState.Never)] 
public struct WgpuBufferCache
{
    private CacheEntry      group0;
    private CacheEntry      group1;
    private int             lruIndex;
    
    public override string  ToString() => $"lru: {(lruIndex == 0 ? ">[0]< [1] " : " [0] >[1]<")}  |  group0: {group0}  |  group1: {group1}";
    
    public WgpuBindGroup GetGroup(ulong groupHash)
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

    internal void Update(WgpuBindGroup bindGroup, ulong hash)
    {
        lruIndex = lruIndex == 1 ? 0 : 1;
        if (lruIndex == 0) {
            group0.Update(bindGroup, hash);
        } else {
            group1.Update(bindGroup, hash);
        }
    }
    
    internal void Release() {
        group0.Release();
        group1.Release();
    }
}


[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct WgpuComputePipeline
{
    internal readonly   ComputePipeline*    handle;
    public   override   string              ToString() => handle != null ? "Created" : "null";
    
    internal WgpuComputePipeline(ComputePipeline* handle) {
        this.handle = handle;
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct WgpuShaderModule
{
    internal readonly   ShaderModule*   handle;
    public   override   string          ToString() => handle != null ? "Created" : "null";
    
    internal WgpuShaderModule(ShaderModule* handle) {
        this.handle = handle;
    }
}


internal readonly unsafe struct StagingReadBuffer
{
    internal readonly   Buffer* handle;
    internal readonly   int     size;
    
    internal StagingReadBuffer(Buffer* handle, int size) {
        this.handle = handle;
        this.size   = size;
    }
}

internal class StagingPool
{
    // used ConcurrentQueue<T> in favor of ConcurrentStack<T>
    private readonly ConcurrentQueue<StagingReadBuffer> pooled = [];

    public  override    string  ToString() => $"pooled: {pooled.Count}";

    internal  StagingReadBuffer Fetch(WgpuDevice device)
    {
        if (pooled.TryDequeue(out var list)) {
            return list;
        }
        return device.CreateStagingBuffer(16 * 1024 * 1024, "staging_buffer");
    }

    internal void Return(StagingReadBuffer list)
    {
        pooled.Enqueue(list);
        // ConcurrentStack<CommandList>.Push() allocates Node -> 40 bytes 
    }
}