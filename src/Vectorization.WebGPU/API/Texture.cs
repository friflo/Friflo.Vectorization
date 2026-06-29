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
    private readonly    GpuTextureDescriptor        desc;
    private readonly    WgpuDevice                  device;
    private             Texture*                    handle;
    private             GpuTextureViewDescriptor[]  viewDescriptors = [];
    private             nint[]                      viewHandles     = [];
    private             int                         viewCount;
    
    public              string                      Label       => desc.label;
    public ref readonly GpuTextureDescriptor        Descriptor  => ref desc;
    public              bool                        IsDisposed  => handle == null;
    public  override    string                      ToString()  => Label;
    
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
    
    public GpuTextureView CreateView(in GpuTextureViewDescriptor? descriptor = null)
    {
        var inDesc   = descriptor ?? new GpuTextureViewDescriptor();
        var viewDesc = new TextureViewDescriptor {
            dimension       = inDesc.dimension,
            format          = inDesc.format == TextureFormat.Undefined  ? desc.format : inDesc.format,
            mipLevelCount   = (uint)(inDesc.mipLevelCount    == 0 ? 1 : inDesc.mipLevelCount),
            arrayLayerCount = (uint)(inDesc.arrayLayerCount  == 0 ? 1 : inDesc.arrayLayerCount),
            aspect          = inDesc.aspect == TextureAspect.Undefined ? TextureAspect.All : inDesc.aspect
        };
        var label = inDesc.label ?? Label;
        inDesc.label = null;
        
        var index = Array.IndexOf(viewDescriptors, inDesc);
        if (index >= 0) {
            return new GpuTextureView((TextureView*)viewHandles[index], this);
        }
        
        var labelMaxCount   = WgpuUtils.GetMaxCount(label);
        var labelBuffer     = stackalloc byte[labelMaxCount];
        viewDesc.label      = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        var view = wgpuTextureCreateView(handle, &viewDesc);
        
        if (viewCount >= viewHandles.Length) {
            viewHandles     = WgpuUtils.Resize(ref viewHandles,     viewCount + 1);
            viewDescriptors = WgpuUtils.Resize(ref viewDescriptors, viewCount + 1);
        }
        viewHandles    [viewCount] = (nint)view;
        viewDescriptors[viewCount] = inDesc;
        viewCount++;
        return new GpuTextureView(view, this);
    }
}

