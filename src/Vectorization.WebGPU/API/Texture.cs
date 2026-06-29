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


public unsafe class GpuTexture : IDisposable
{
    private readonly    TextureDescriptor   desc;
    public  readonly    string              Label;
    private readonly    WgpuDevice          device;
    private             Texture*            handle;
    private             ViewEntry[]         viewEntries = [];
    private             nint[]              viewHandles = [];
    private             int                 viewCount;
    
    public ref readonly TextureDescriptor   Descriptor  => ref desc;
    public              bool                IsDisposed  => handle == null;
    public  override    string              ToString()  => Label;
    
    internal GpuTexture(WgpuDevice device, TextureDescriptor desc, Texture* handle, string label)
    {
        this.device = device;
        this.desc  	= desc;
        Label       = label;
        this.handle = handle;
        this.desc.label = default;
    }

    public void Write(
        ReadOnlySpan<byte>  data,
        int                 bytesPerRow,
        int                 rowsPerImage,
        int                 mipLevel    = 0,
        int                 offset      = 0,
        Origin3D            origin      = default,
        TextureAspect       aspect      = TextureAspect.All,
        Extent3D?           writeSize   = null)
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
        var finalWriteSize = writeSize ?? new Extent3D {
            width               = desc.size.width,
            height              = desc.size.height,
            depthOrArrayLayers  = desc.size.depthOrArrayLayers
        };
        fixed (byte* dataPtr = data) {
            wgpuQueueWriteTexture(device.QueuePtr, &destination, dataPtr, (nuint)data.Length, &sourceLayout, &finalWriteSize);
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
        internal readonly   TextureSampleType       sampleType;
        // --- TextureViewDescriptor
        internal readonly   TextureFormat           format;
        internal readonly   TextureViewDimension    dimension;
        internal readonly   uint                    baseMipLevel;
        internal readonly   uint                    mipLevelCount;
        internal readonly   uint                    baseArrayLayer;
        internal readonly   uint                    arrayLayerCount;
        internal readonly   TextureAspect           aspect;
        internal readonly   TextureUsage            usage;

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
            usage           = (TextureUsage)descriptor.usage;
        }
    }
    
    public GpuTextureView CreateView(in TextureViewDescriptor descriptor = default)
    {
        var view = CreateView(descriptor, TextureViewDimension.D2D, GetType<float>());
        return new GpuTextureView(view, this);
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


/// <summary>
/// Names of struct types implementing <see cref="ITextureView"/> define the <see cref="BindGroupLayoutEntry.texture"/>
/// </summary>
/// <remarks>
/// Bind group layout creation:<br/>
/// <see cref="BindGroupLayoutEntry"/>'s are used to create a <see cref="BindGroupLayoutDescriptor"/>.<br/>
/// The descriptor is used to create a <see cref="BindGroupLayout"/> handle with <see cref="wgpuDeviceCreateBindGroupLayout"/>.<br/>
/// <br/>
/// Bind group creation:<br/>
/// The <see cref="BindGroupLayout"/> handle is used in <see cref="BindGroupDescriptor.entries"/> to create a <see cref="BindGroup"/> handle.<br/>
/// These <see cref="BindGroupDescriptor.entries"/> are of type <see cref="BindGroupEntry"/>.<br/> 
/// A <see cref="BindGroupEntry.textureView"/> can be assigned with <see cref="ITextureView.Handle"/><br/>
/// <br/>
/// Important for understanding:<br/>
/// A <see cref="TextureView"/>* defines an immutable configuration state created with <see cref="wgpuTextureCreateView"/>.<br/>
/// <br/>
/// <see cref="TextureBindingLayout"/> fields used in <see cref="BindGroupLayoutEntry.texture"/>:<br/>
/// - <see cref="TextureBindingLayout.sampleType"/><br/>
/// - <see cref="TextureBindingLayout.viewDimension"/><br/>
/// - <see cref="TextureBindingLayout.multisampled"/><br/>
/// </remarks>
public interface ITextureView
{
    nint Handle { get; }
}
