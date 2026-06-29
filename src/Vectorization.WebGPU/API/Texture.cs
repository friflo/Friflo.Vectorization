// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable NotAccessedField.Local
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


public sealed unsafe class GpuTexture : IDisposable
{
    private readonly    GpuTextureDescriptor    desc;
    private readonly    WgpuDevice              device;
    private             Texture*                handle;
    private             ViewEntry[]             viewEntries = [];
    private             nint[]                  viewHandles = [];
    private             int                     viewCount;
    
    public              string                  Label       => desc.label;
    public ref readonly GpuTextureDescriptor    Descriptor  => ref desc;
    public              bool                    IsDisposed  => handle == null;
    public  override    string                  ToString()  => Label;
    
    internal GpuTexture(WgpuDevice device, in GpuTextureDescriptor desc, Texture* handle)
    {
        this.device = device;
        this.desc  	= desc;
        this.handle = handle;
    }

    public void Write(
        ReadOnlySpan<byte>  data,
        int                 bytesPerRow,
        int                 rowsPerImage,
        int                 mipLevel    = 0,
        int                 offset      = 0,
        Origin3D            origin      = default,
        TextureAspect       aspect      = TextureAspect.All,
        TextureSize?        writeSize   = null)
    {
        var destination = new TexelCopyTextureInfo {
            texture         = handle,
            mipLevel        = (uint)mipLevel,
            origin          = origin,
            aspect          = aspect
        };
        var sourceLayout = new TexelCopyBufferLayout {
            offset          = (uint)offset,
            bytesPerRow     = (uint)bytesPerRow,
            rowsPerImage    = (uint)rowsPerImage
        };
        writeSize ??= desc.size;
        var extent3D = new Extent3D {
            height              = (uint)writeSize.Value.height,
            width               = (uint)writeSize.Value.width,
            depthOrArrayLayers  = (uint)writeSize.Value.depthOrArrayLayers
        };

        fixed (byte* dataPtr = data) {
            wgpuQueueWriteTexture(device.QueuePtr, &destination, dataPtr, (nuint)data.Length, &sourceLayout, &extent3D);
        }
    }
    
    public void Dispose()
    {
        for (int n = 0; n < viewCount; n++) {
            wgpuTextureViewRelease((TextureView*)viewHandles[n]);
        }
        viewCount = 0;
        
        if (handle != null) {
            wgpuTextureRelease(handle);
            handle = null;
        }
    }
    
    private readonly record struct ViewEntry
    {
        internal readonly   TextureFormat           format;
        internal readonly   TextureViewDimension    dimension;
        internal readonly   uint                    baseMipLevel;
        internal readonly   uint                    mipLevelCount;
        internal readonly   uint                    baseArrayLayer;
        internal readonly   uint                    arrayLayerCount;
        internal readonly   TextureAspect           aspect;
        internal readonly   TextureUsage            usage;

        public ViewEntry(in TextureViewDescriptor descriptor)
        {
            format          = descriptor.format;
            dimension       = descriptor.dimension;
            baseMipLevel    = descriptor.baseMipLevel;
            mipLevelCount   = descriptor.mipLevelCount;
            baseArrayLayer  = descriptor.baseArrayLayer;
            arrayLayerCount = descriptor.arrayLayerCount;
            aspect          = descriptor.aspect;
            usage           = (TextureUsage)descriptor.usage;
        }
    }
    
    public GpuTextureView CreateView(in TextureViewDescriptor descriptor = default)
    {
        var view = CreateView(descriptor, TextureViewDimension.D2D);
        return new GpuTextureView(view, this);
    }

    
    internal TextureView* CreateView(TextureViewDescriptor viewDesc, TextureViewDimension dimension)
    {
        viewDesc.dimension          = dimension;
        viewDesc.format             = viewDesc.format           == TextureFormat.Undefined  ? desc.format : viewDesc.format;
        viewDesc.mipLevelCount      = viewDesc.mipLevelCount    == 0 ? 1 : viewDesc.mipLevelCount;
        viewDesc.arrayLayerCount    = viewDesc.arrayLayerCount  == 0 ? 1 : viewDesc.arrayLayerCount;
        viewDesc.aspect             = viewDesc.aspect           == TextureAspect.Undefined ? TextureAspect.All : viewDesc.aspect;
        
        var entry = new ViewEntry(viewDesc);
        
        var index = viewEntries.IndexOf(entry);
        if (index >= 0) {
            return (TextureView*)viewHandles[index];
        }
        
        var labelMaxCount   = WgpuUtils.GetMaxCount(Label);
        var labelBuffer     = stackalloc byte[labelMaxCount];
        viewDesc.label      = WgpuUtils.CopyToStringView(Label, labelBuffer, labelMaxCount);
        
        var view = wgpuTextureCreateView(handle, &viewDesc);
        
        if (viewCount >= viewHandles.Length) {
            viewHandles = WgpuUtils.Resize(ref viewHandles, viewCount + 1);
            viewEntries = WgpuUtils.Resize(ref viewEntries, viewCount + 1);
        }
        viewHandles[viewCount] = (nint)view;
        viewEntries[viewCount] = entry;
        viewCount++;
        return view;
    }
}

