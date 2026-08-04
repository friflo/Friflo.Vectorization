// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime {


/// <summary>
/// Caches the <see cref="computePipeline"/>, <see cref="layouts"/>
/// and the <see cref="WgpuBindGroup"/>'s.
/// for a specific <see cref="RenderConfig"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct ComputeCache
{
    public   readonly   BindGroupCache          bindGroupCache;     //  8
    public   readonly   WgpuComputePipeline     computePipeline;    //  8
    public   readonly   WgpuBindGroupLayout4    layouts;            // 32
    internal readonly   ulong                   wgslHash;           //  8
    
    public              bool                    IsCreated   => computePipeline.handle != null;
    public   override   string                  ToString()  => computePipeline.handle != null ? "Created" : "null";

    internal ComputeCache (
        ulong                   wgslHash,
        WgpuComputePipeline     computePipeline,
        BindGroupCache          bindGroupCache,
        WgpuBindGroupLayout     bufferLayout,
        WgpuBindGroupLayout     uniformLayout)
    {
        this.wgslHash           = wgslHash;
        this.computePipeline    = computePipeline;
        this.bindGroupCache     = bindGroupCache;
        layouts[0]              = bufferLayout;
        layouts[1]              = uniformLayout;
    }
    
    internal ComputeCache (
        ulong                               wgslHash,
        WgpuComputePipeline                 computePipeline,
        BindGroupCache                      bindGroupCache,
        ReadOnlySpan<WgpuBindGroupLayout>   layouts)
    {
        this.wgslHash           = wgslHash;
        this.computePipeline    = computePipeline;
        this.bindGroupCache     = bindGroupCache;
        for (int n = 0; n < layouts.Length; n++) {
            this.layouts[n] = layouts[n];
        }
    }
}

}



namespace Friflo.Vectorization.WebGPU {

public sealed partial  class WgpuDevice
{
    // --------------------- computeCacheSlots ---------------------
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ref readonly ComputeCache GetPipelineCache(int slot, ulong wgslHash)
    {
        var slots = computeCacheSlots;
        if (slot < slots.Length) {
            ref var cache = ref slots[slot];
            if (cache.wgslHash == wgslHash) {
                return ref cache;
            }
        }
        return ref MissingComputeCache;
    }
    
    private static readonly ComputeCache MissingComputeCache = default;
    
    public ref readonly ComputeCache CreateComputeCache(
        int                 kernelId,
        ulong               wgslHash,
        WgpuComputePipeline computePipeline,
        WgpuBindGroupLayout bufferLayout,
        WgpuBindGroupLayout uniformLayout,
        BindGroupCache      bindGroupCache)
    {
        var slots = computeCacheSlots;
        if (kernelId >= slots.Length) {
            slots = WgpuUtils.ResizeInit(ref computeCacheSlots, kernelId + 1);
        }
        ref var cache = ref slots[kernelId];
        cache = new ComputeCache(wgslHash, computePipeline, bindGroupCache, bufferLayout, uniformLayout);
        return ref cache;
    }
    
    public ref readonly ComputeCache CreateComputeCache(
        int                                         kernelId,
        ulong                                       wgslHash,
        WgpuComputePipeline                         computePipeline,
        scoped ReadOnlySpan<WgpuBindGroupLayout>    layouts,
        BindGroupCache                              bindGroupCache)
    {
        var slots = computeCacheSlots;
        if (kernelId >= slots.Length) {
            slots = WgpuUtils.ResizeInit(ref computeCacheSlots, kernelId + 1);
        }
        ref var cache = ref slots[kernelId];
        cache = new ComputeCache(wgslHash, computePipeline, bindGroupCache, layouts);
        return ref cache;
    }
}

}
