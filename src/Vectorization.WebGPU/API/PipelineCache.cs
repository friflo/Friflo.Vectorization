// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


[EditorBrowsable(EditorBrowsableState.Never)]
internal struct PipelineCaches
{
    internal PipelineCache[] caches = [];

    public override string ToString() => $"length: {caches.Length}";

    public PipelineCaches() { }
}

/// <summary>
/// Caches the <see cref="renderPipeline"/>, the <see cref="layouts"/> and the <see cref="WgpuBindGroup"/>'s
/// for a specific <see cref="RenderConfig"/>.
/// </summary>
public readonly unsafe struct PipelineCache
{
    public   readonly   BindGroupCache          bindGroupCache;
    public   readonly   WgpuRenderPipeline      renderPipeline;
    public   readonly   WgpuBindGroupLayout[]   layouts = new WgpuBindGroupLayout[4]; // TODO change to inline array
    internal readonly   ulong                   wgslHash;
    public              bool                    IsCreated => renderPipeline.handle != null;

    public   override   string                  ToString()=> renderPipeline.handle != null ? "Created" : "null";

    internal PipelineCache (ulong wgslHash, WgpuRenderPipeline renderPipeline, BindGroupCache bindGroupCache)
    {
        this.wgslHash       = wgslHash;
        this.renderPipeline = renderPipeline;
        this.bindGroupCache = bindGroupCache;
    }
}

public abstract class BindGroupCache
{
    protected internal abstract void Clear();
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected static unsafe void ReleaseBindGroups<TKey>(Dictionary<TKey, WgpuBindGroup> bindGroups) where TKey : unmanaged
    {
        foreach (var bindGroup in bindGroups.Values) {
            wgpuBindGroupRelease(bindGroup.handle);
        }
        bindGroups.Clear();
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected static unsafe void ReleaseBindGroup(ref WgpuBindGroup bindGroup)
    {
        if (bindGroup.handle != null) {
            wgpuBindGroupRelease(bindGroup.handle);
        }
        bindGroup = default;
    }
}

public sealed partial  class WgpuDevice
{
    // --------------------- shaderEffectSlots ---------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref PipelineCache GetPipelineCache(int slot, RenderConfig config, ulong wgslHash)
    {
        var slots = pipelineCacheSlots;
        if (slot < slots.Length) {
            var caches      = slots[slot].caches;
            var configId    = config.Id;
            if (configId < caches.Length)
            {
                ref var cache = ref caches[slot]; 
                if (cache.wgslHash == wgslHash) {
                    return ref cache;
                }
            }
        }
        return ref MissingPipelineCache;
    }
    
    private static PipelineCache MissingPipelineCache;
    
    public ref PipelineCache CreatePipelineCache(
        int                                         kernelId,
        RenderConfig                                config,
        ulong                                       wgslHash,
        WgpuRenderPipeline                          renderPipeline,
        scoped ReadOnlySpan<WgpuBindGroupLayout>    layouts,
        BindGroupCache                              bindGroupCache)
    {
        var slots = pipelineCacheSlots;
        if (kernelId >= slots.Length) {
            slots = WgpuUtils.ResizeInit(ref pipelineCacheSlots, kernelId + 1);
        }
        ref var slotCaches = ref slots[kernelId];
        var caches      = slotCaches.caches;
        var configId    = config.Id;
        if (configId >= caches.Length) {
            caches = WgpuUtils.Resize(ref slotCaches.caches, configId + 1);
        }
        ref var cache = ref caches[configId];
        cache = new PipelineCache(wgslHash, renderPipeline, bindGroupCache);
        
        for (int n = 0; n < layouts.Length; n++) {
            cache.layouts[n] = layouts[n];
        }
        return ref cache;
    }
}

