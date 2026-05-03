// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Text;
using Silk.NET.WebGPU;

namespace Friflo.Vectorization.GPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct GpuEffect 
{
    internal readonly   GpuBindGroupLayout  layout;
    internal readonly   GpuShaderModule     shaderModule;
    internal readonly   GpuComputePipeline  pipeline;
    
    public              bool                IsCreated => layout.handle != null;
    
    public GpuEffect (GpuBindGroupLayout layout, GpuShaderModule shaderModule, GpuComputePipeline pipeline) {
        this.layout         = layout;
        this.shaderModule   = shaderModule;
        this.pipeline       = pipeline;
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

internal static class GpuUtils
{
    internal static int GetMaxCount(ReadOnlySpan<char> span)
    {
        return span.IsEmpty ? 1 : Encoding.UTF8.GetMaxByteCount(span.Length) + 1; // + \0
    }
    
    internal static unsafe void CopySpanToBuffer(ReadOnlySpan<char> span, byte* destBuffer, int destLength)
    {
        if (span.IsEmpty) {
            destBuffer[0] = 0;
            return;
        }
        var dest = new Span<byte>(destBuffer, destLength);
        int actualByteCount = Encoding.UTF8.GetBytes(span, dest);
        destBuffer[actualByteCount] = 0; // Null-Terminator
    }
}
