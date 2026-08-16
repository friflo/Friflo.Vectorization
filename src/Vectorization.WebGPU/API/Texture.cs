// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable UnusedMember.Local
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable NotAccessedField.Local
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


public sealed unsafe class GpuTexture : IDisposable
{
    private             Texture*                    handle;
    private readonly    WgpuDevice                  device;
    private readonly    GpuTextureDescriptor        desc;
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
        GpuExtent3D?        writeSize   = null)
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
    
    /// <summary> Creates a <see cref="GpuTextureView"/> representing a specific view of the GPUTexture. </summary>
    /// <remarks>
    /// <remarks>Same behavior as: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPUTexture/createView">MDN: GPUTexture.createView()</a></remarks>
    /// </remarks>
    public GpuTextureView CreateView(in GpuTextureViewDescriptor? descriptor = null)
    {
        var inDesc = descriptor ?? new GpuTextureViewDescriptor();
        
        // Resolve texture view dimension based on texture type if undefined
        if (inDesc.dimension == TextureViewDimension.Undefined) {
            inDesc.dimension = desc.dimension switch {
                TextureDimension.D1D    => TextureViewDimension.D1D,
                TextureDimension.D2D    => TextureViewDimension.D2D,
                TextureDimension.D3D    => TextureViewDimension.D3D,
                _                       => TextureViewDimension.D2D
            };
        }
        // 0 means: use all remaining mip levels
        inDesc.mipLevelCount = inDesc.mipLevelCount == 0        ? desc.mipLevelCount - inDesc.baseMipLevel 
                                                                : inDesc.mipLevelCount;
        // 0 means: use all remaining array layers
        inDesc.arrayLayerCount = inDesc.arrayLayerCount == 0    ? desc.size.depthOrArrayLayers - inDesc.baseArrayLayer
                                                                : inDesc.arrayLayerCount;
        
        inDesc.format = inDesc.format == TextureFormat.Undefined ? desc.format : inDesc.format;

        var nativeDesc = new TextureViewDescriptor {
            nextInChain     = (ChainedStruct*)inDesc.nextInChain,
            dimension       = inDesc.dimension,
            format          = inDesc.format,
            baseMipLevel    = (uint)inDesc.baseMipLevel,
            mipLevelCount   = (uint)inDesc.mipLevelCount,
            baseArrayLayer  = (uint)inDesc.baseArrayLayer,
            arrayLayerCount = (uint)inDesc.arrayLayerCount,
            aspect          = inDesc.aspect,
            usage           = (ulong)inDesc.usage 
        };
        var label = inDesc.label ?? Label;
        inDesc.label = null;
        
        var index = Array.IndexOf(viewDescriptors, inDesc);
        if (index >= 0) {
            return new GpuTextureView((TextureView*)viewHandles[index], this);
        }
        
        var labelMaxCount   = WgpuUtils.GetMaxCount(label);
        var labelBuffer     = stackalloc byte[labelMaxCount];
        nativeDesc.label      = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        var view = wgpuTextureCreateView(handle, &nativeDesc);
        
        if (viewCount >= viewHandles.Length) {
            viewHandles     = WgpuUtils.Resize(ref viewHandles,     viewCount + 1);
            viewDescriptors = WgpuUtils.Resize(ref viewDescriptors, viewCount + 1);
        }
        viewHandles    [viewCount] = (nint)view;
        viewDescriptors[viewCount] = inDesc;
        viewCount++;
        return new GpuTextureView(view, this);
    }
    
    internal GpuTextureViewDescriptor FindViewDescriptor(TextureView* view)
    {
        var index = viewHandles.IndexOf((nint)view);
        return viewDescriptors[index];
    }
}

/// <summary> manage type for:  <see cref="TextureViewDescriptor"/>. </summary>
public record struct GpuTextureViewDescriptor
{
    public  nint                    nextInChain;
    public  string                  label;
    public  TextureFormat           format              = TextureFormat.Undefined;
    public  TextureViewDimension    dimension           = TextureViewDimension.Undefined;
    public  int                     baseMipLevel;
    public  int                     mipLevelCount;      // 0: use all remaining mip levels
    public  int                     baseArrayLayer;
    public  int                     arrayLayerCount;    // 0: use all remaining array layers
    public  TextureAspect           aspect              = TextureAspect.All;
    public  TextureUsage            usage;
    
    public GpuTextureViewDescriptor() { }
}


/// <summary>
/// When used as a shader method parameter the parameter must have a <see cref="TextureTypeAttribute"/>.<br/>
/// The texture view os owned by its <see cref="GpuTexture"/> and is disposed when the texture is disposed.
/// </summary>
public readonly unsafe struct GpuTextureView
{
    internal readonly   TextureView*                handle;
    private  readonly   GpuTexture                  texture;
    private             GpuTextureViewDescriptor    Descriptor => texture.FindViewDescriptor(handle); // only for debugging
    public              bool                        IsDisposed => texture.IsDisposed;

    public   override   string          ToString() => texture?.Label; 

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

