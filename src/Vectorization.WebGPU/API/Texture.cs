// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable UnusedTypeParameter
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


public abstract unsafe class GpuTexture(WgpuDevice device, TextureDescriptor desc, Texture* handle) : IDisposable
{
    private             Texture*    handle      = handle;
    private readonly    List<nint>  viewList    = new();
    
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
        foreach (var view in viewList) {
            wgpuTextureViewRelease((TextureView*)view);
        }
        viewList.Clear();
        
        if (handle != null) {
            wgpuTextureRelease(handle);
            handle = null;
        }
    }
    
    internal TextureView* CreateView(TextureViewDescriptor viewDesc, TextureViewDimension dimension)
    {
        viewDesc.dimension =  dimension;
        var view = wgpuTextureCreateView(handle, &viewDesc);
        viewList.Add((nint)view);
        return view;
    }
}

public sealed class GpuTexture2D : GpuTexture
{
    internal unsafe GpuTexture2D(WgpuDevice device, in TextureDescriptor desc, Texture* handle) : base(device, desc, handle) { }
    
    public unsafe texture_2d<T> texture_2d<T>(in TextureViewDescriptor desc) where T : unmanaged
    {
        return new texture_2d<T>(CreateView(desc, TextureViewDimension.D2D), this);
    }
}

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

public readonly unsafe struct texture_2d<T>(TextureView* handle, GpuTexture texture) : ITextureView where T : unmanaged
{
    public TextureViewHandle Handle => new TextureViewHandle(handle, texture);
}




