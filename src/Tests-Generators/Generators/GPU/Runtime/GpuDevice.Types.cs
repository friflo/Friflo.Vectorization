// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.ComponentModel;
using Silk.NET.WebGPU;

// file contains structs created by:  GpuDevice
namespace Friflo.Vectorization.GPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct GpuEffect 
{
    internal readonly   GpuBindGroupLayout  layout;
    internal readonly   GpuComputePipeline  pipeline;
    public              bool                IsCreated => layout.handle != null;

    public   override   string              ToString()=> layout.handle != null ? "Created" : "null";

    internal GpuEffect (GpuBindGroupLayout layout, GpuComputePipeline pipeline) {
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
