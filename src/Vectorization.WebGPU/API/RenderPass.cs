// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable CheckNamespace

using System;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;

namespace Friflo.Vectorization.WebGPU;

[AttributeUsage(AttributeTargets.Method)]
public sealed class ShaderAttribute<T> : Attribute where T : struct
{
    public ShaderAttribute (string wgsl) { }
}

public static class WgpuExtensions
{
    public static RenderFrame BeginFrame(this PipelineContext context) {
        return new RenderFrame();
    }
}

public struct RenderFrame : IDisposable
{
    public RenderPass<T> BeginRenderPass<T>(RenderPassColorAttachment attachment) where T : struct
    {
        return default;
    }
    
    public void Dispose() { }
}

public readonly struct RenderPass<T> : IDisposable where T : struct
{
    public void Dispose() { }
} 



