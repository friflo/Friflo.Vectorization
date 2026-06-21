// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedParameter.Local
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;

/* [AttributeUsage(AttributeTargets.Method)]
public sealed class ShaderAttribute<TStage> : Attribute where TStage : struct
{
    public ShaderAttribute (string wgsl) { }
} */

[AttributeUsage(AttributeTargets.Method)]
public sealed class ShaderAttribute : Attribute
{
    public ShaderAttribute (string wgsl) { }
}

// --- Generator Draw Call Rules ---
// 1. [BindIndex]  present              -> pass.DrawIndexed(indices.Length, [BindInstance] ?? 1, 0, 0, 0);
// 2. [BindVertex] only                 -> pass.Draw(vertices.Length, [BindInstance] ?? 1, 0, 0);
// 3. No geometry (Fullscreen/Compute)  -> pass.Draw(3, 1, 0, 0);

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BindVertexAttribute : Attribute
{
    public BindVertexAttribute (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BindUniformAttribute : Attribute
{
    public BindUniformAttribute (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BindTextureAttribute : Attribute
{
    /// enums are used by <see cref="TextureBindingLayout"/>
    public BindTextureAttribute (
        int                     groupIndex,
        int                     bindingIndex,
        TextureSampleType       sampleType      = TextureSampleType.Float,
        TextureViewDimension    viewDimension   = TextureViewDimension.D2D,
        bool                    multisampled    = false) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BindSamplerAttribute : Attribute
{
    /// type is used by <see cref="SamplerBindingLayout"/>
    public BindSamplerAttribute (
        int                     groupIndex,
        int                     bindingIndex,
        SamplerBindingType      type            = SamplerBindingType.Filtering) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BindStorageAttribute : Attribute
{
    public BindStorageAttribute (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BindIndexAttribute : Attribute { }






// --- skeletons
public abstract unsafe class GpuTexture : IDisposable
{
    internal Texture* handle;
    
    protected GpuTexture(Texture* handle) {
        this.handle = handle;
    }
    
    public void Dispose()
    {
        if (handle != null) {
            wgpuTextureRelease(handle);
            handle = null;
        }
    }
}

public sealed class GpuTexture2D :  GpuTexture
{
    internal unsafe GpuTexture2D(Texture* handle) : base(handle) { }
}




public sealed unsafe class GpuSampler : IDisposable
{
    internal Sampler* handle;
    
    internal GpuSampler(Sampler* handle) {
        this.handle = handle;
    }
    
    
    public void Dispose()
    {
        if (handle != null) {
            wgpuSamplerRelease(handle);
            handle = null;
        }
    }
}
