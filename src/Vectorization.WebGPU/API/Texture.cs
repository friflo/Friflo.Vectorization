// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;
// ReSharper disable InconsistentNaming
// ReSharper disable NotAccessedField.Local

// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


public abstract unsafe class GpuTexture(WgpuDevice device, TextureDescriptor desc, Texture* handle) : IDisposable
{
    private             Texture*        handle      = handle;
    private readonly    List<ViewEntry> viewEntries = [];
    private readonly    List<nint>      viewHandles = [];
    
    
    public void Write(TexelCopyTextureInfo destination, ReadOnlySpan<byte> data, TexelCopyBufferLayout dataLayout, Extent3D writeSize = default)
    {
        if (writeSize.width == 0 && writeSize.height == 0 && writeSize.depthOrArrayLayers == 0) {
            writeSize = desc.size;
        }
        destination.texture = handle;
        if (destination.aspect == 0) {
            destination.aspect = TextureAspect.All;
        }
        fixed (byte* dataPtr = data) {
            wgpuQueueWriteTexture(device.QueuePtr, &destination, dataPtr, (nuint)data.Length, &dataLayout, &writeSize);
        }
    }
    
    public void Dispose()
    {
        foreach (var viewHandle in viewHandles) {
            wgpuTextureViewRelease((TextureView*)viewHandle);
        }
        viewHandles.Clear();
        viewEntries.Clear();
        
        if (handle != null) {
            wgpuTextureRelease(handle);
            handle = null;
        }
    }
    
    private readonly record struct ViewEntry
    {
        internal readonly   TextureSampleType       sampleType;
        // --- TextureViewDescriptor
        internal readonly   TextureFormat           format;
        internal readonly   TextureViewDimension    dimension;
        internal readonly   uint                    baseMipLevel;
        internal readonly   uint                    mipLevelCount;
        internal readonly   uint                    baseArrayLayer;
        internal readonly   uint                    arrayLayerCount;
        internal readonly   TextureAspect           aspect;
        internal readonly   ulong                   usage;

        public ViewEntry(in TextureViewDescriptor descriptor, TextureSampleType sampleType)
        {
            this.sampleType = sampleType;
            format          = descriptor.format;
            dimension       = descriptor.dimension;
            baseMipLevel    = descriptor.baseMipLevel;
            mipLevelCount   = descriptor.mipLevelCount;
            baseArrayLayer  = descriptor.baseArrayLayer;
            arrayLayerCount = descriptor.arrayLayerCount;
            aspect          = descriptor.aspect;
            usage           = descriptor.usage;
        }
    }

    
    internal TextureView* CreateView(TextureViewDescriptor viewDesc, TextureViewDimension dimension, TextureSampleType sampleType)
    {
        viewDesc.dimension          = dimension;
        viewDesc.format             = viewDesc.format           == TextureFormat.Undefined  ? desc.format : viewDesc.format;
        viewDesc.mipLevelCount      = viewDesc.mipLevelCount    == 0 ? 1 : viewDesc.mipLevelCount;
        viewDesc.arrayLayerCount    = viewDesc.arrayLayerCount  == 0 ? 1 : viewDesc.arrayLayerCount;
        viewDesc.aspect             = viewDesc.aspect           == TextureAspect.Undefined ? TextureAspect.All : viewDesc.aspect;
        
        var entry = new ViewEntry(viewDesc, sampleType);
        
        var index = viewEntries.IndexOf(entry);
        if (index >= 0) {
            return (TextureView*)viewHandles[index];
        }
        var view = wgpuTextureCreateView(handle, &viewDesc);
        
        viewHandles.Add((nint)view);
        viewEntries.Add(entry);
        return view;
    }
    
    internal static TextureSampleType GetType<T>() where T : unmanaged
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

public readonly unsafe struct TextureViewHandle
{
    internal readonly   TextureView*    handle;
    internal readonly   GpuTexture      texture;
    
    internal TextureViewHandle (TextureView* handle, GpuTexture texture) {
        this.handle     = handle;
        this.texture    = texture;
    }
}

public interface ITextureView
{
    TextureViewHandle Handle { get; }
}


