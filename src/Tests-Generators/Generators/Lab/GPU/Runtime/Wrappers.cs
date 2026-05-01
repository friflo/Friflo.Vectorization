// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.ComponentModel;
using Friflo.Vectorization.GPU;
using Silk.NET.WebGPU;

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct GpuEffect 
{
    public readonly GpuBindGroupLayout  layout;
    public readonly GpuComputePipeline  pipeline;
    public          bool                IsCreated => layout.handle != null;
    
    public GpuEffect (GpuBindGroupLayout layout, GpuComputePipeline pipeline) {
        this.layout     = layout;
        this.pipeline   = pipeline;
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct GpuComputePipeline
{
    internal readonly ComputePipeline* handle;
    
    internal GpuComputePipeline(ComputePipeline* handle) {
        this.handle = handle;
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct GpuShaderModule
{
    internal readonly ShaderModule* handle;
    
    internal GpuShaderModule(ShaderModule* handle) {
        this.handle = handle;    
    }
}
