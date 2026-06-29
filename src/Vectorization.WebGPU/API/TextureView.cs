// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.WebGPU.Runtime;


// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;

public readonly unsafe struct GpuTextureView
{
    internal readonly   TextureView*    handle;
    private  readonly   GpuTexture      texture;
    
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
