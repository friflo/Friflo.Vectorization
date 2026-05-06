// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.ComponentModel;
using Silk.NET.WebGPU;

// file contains structs created by:  GpuDevice
namespace Friflo.Vectorization.GPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public unsafe struct GpuEffect 
{
    internal readonly   GpuComputePipeline  pipeline;
    internal readonly   GpuBindGroupLayout  bufferLayout;
    internal readonly   GpuBindGroupLayout  uniformLayout;
    public              bool                IsCreated => bufferLayout.handle != null;

    public   override   string              ToString()=> bufferLayout.handle != null ? "Created" : "null";

    internal GpuEffect (GpuComputePipeline pipeline, GpuBindGroupLayout  bufferLayout, GpuBindGroupLayout uniformLayout) {
        this.pipeline       = pipeline;
        this.bufferLayout   = bufferLayout;
        this.uniformLayout  = uniformLayout;
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
