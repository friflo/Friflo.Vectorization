// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable UnassignedField.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;

/// <summary> manage type for:  <see cref="TextureViewDescriptor"/>. </summary>
public record struct GpuTextureViewDescriptor
{
    public  nint                    nextInChain;
    public  string                  label;
    public  TextureFormat           format;
    public  TextureViewDimension    dimension       = TextureViewDimension.D2D;
    public  int                     baseMipLevel;
    public  int                     mipLevelCount;
    public  int                     baseArrayLayer;
    public  int                     arrayLayerCount;
    public  TextureAspect           aspect;
    public  TextureUsage            usage;
    
    public GpuTextureViewDescriptor() { }
}

/// <summary>
/// When used as a shader method parameter the parameter must have a <see cref="SamplerAttribute"/>.
/// </summary>
public readonly unsafe struct GpuTextureView
{
    internal readonly   TextureView*    handle;
    private  readonly   GpuTexture      texture;

    public   override   string          ToString() => texture.Label; 

    public nint Handle {
        get {
            if (texture.IsDisposed) {
                ThrowObjectDisposedException();
            }
            return (nint)handle;
        }
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)] [StackTraceHidden] [DoesNotReturn]
    private void ThrowObjectDisposedException()
    {
        throw new ObjectDisposedException($"texture view of disposed GpuTexture '{texture.Label}'");
    }

    internal GpuTextureView(TextureView* view, GpuTexture texture)
    {
        handle          = view;
        this.texture    = texture;
    }
}

