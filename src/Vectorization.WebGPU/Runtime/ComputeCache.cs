// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime {


/// <summary>
/// Caches the <see cref="computePipeline"/>, <see cref="bufferLayout"/>, <see cref="uniformLayout"/>
/// and the <see cref="WgpuBindGroup"/>'s.
/// for a specific <see cref="RenderConfig"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct ComputeCache
{
    public   readonly   BindGroupCache          bindGroupCache;     //  8
    public   readonly   WgpuComputePipeline     computePipeline;    //  8
    public   readonly   WgpuBindGroupLayout     bufferLayout;       //  8
    public   readonly   WgpuBindGroupLayout     uniformLayout;      //  8
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
        this.bufferLayout       = bufferLayout;
        this.uniformLayout      = uniformLayout;
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
    
    public ref readonly ComputeCache CreatePipelineCache(
        int                 kernelId,
        ulong               wgslHash,
        WgpuComputePipeline renderPipeline,
        WgpuBindGroupLayout bufferLayout,
        WgpuBindGroupLayout uniformLayout,
        BindGroupCache      bindGroupCache)
    {
        var slots = computeCacheSlots;
        if (kernelId >= slots.Length) {
            slots = WgpuUtils.ResizeInit(ref computeCacheSlots, kernelId + 1);
        }
        ref var cache = ref slots[kernelId];
        cache = new ComputeCache(wgslHash, renderPipeline, bindGroupCache, bufferLayout, uniformLayout);
        return ref cache;
    }
}

}
