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
    
    // --- TODO attic methods - will be removed
    internal static TextureSampleType GetUnfilterableType<T>() where T : unmanaged
    {
        var sampleType = GetType<T>();
        if (sampleType == TextureSampleType.Float) {
            return TextureSampleType.UnfilterableFloat;
        }
        return sampleType;
    }
    
    internal static TextureSampleType ForceUnfilterable<T>() where T : unmanaged
    {
        var sampleType = GetType<T>();
        if (sampleType == TextureSampleType.Float) {
            return TextureSampleType.UnfilterableFloat;
        }
        return sampleType;
    }
    
    private static TextureSampleType GetType<T>() where T : unmanaged
    {
        var typeCode = Type.GetTypeCode(typeof(T));
        return typeCode switch
        {
            TypeCode.Single => TextureSampleType.Float,
            TypeCode.Int32  => TextureSampleType.Sint,
            TypeCode.UInt32 => TextureSampleType.Uint,
            TypeCode.Object => UnfilterableFloat<T>(),
            _               => throw InvalidType(typeof(T))
        };
    }

    private static ArgumentException InvalidType(Type type)
        => throw new ArgumentException($"invalid type - expect: float, int, uint or UnfilterableFloat. Was: {type.Name}");
    
    private static TextureSampleType UnfilterableFloat<T>() where T : unmanaged
    {
        if (typeof(T) == typeof(UnfilterableFloat)) {
            return TextureSampleType.UnfilterableFloat;
        }
        throw InvalidType(typeof(T));
    }
}

public struct UnfilterableFloat;

