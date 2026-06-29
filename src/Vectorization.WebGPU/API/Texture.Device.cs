// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


[CollectionBuilder(typeof(TextureSizeBuilder), nameof(TextureSizeBuilder.Create))]
public struct TextureSize : IEnumerable<int>
{
    public  int     width;
    public  int     height;
    public  int     depthOrArrayLayers = 1;
    
    public TextureSize() { }
    
    public IEnumerator<int> GetEnumerator() => throw new NotImplementedException();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Internal compiler helper to enable the [...] collection expression for <see cref="TextureSize"/>.
/// </summary>
public static class TextureSizeBuilder
{
    public static TextureSize Create(ReadOnlySpan<int> items)
    {
        var size = new TextureSize();
        if (items.Length > 0) size.width                = items[0];
        if (items.Length > 1) size.height               = items[1];
        if (items.Length > 2) size.depthOrArrayLayers   = items[2];
        return size;
    }
}


/// <summary> <see cref="TextureDescriptor"/> </summary>
public struct GpuTextureDescriptor
{
    public  nint                nextInChain;
    public  string              label;
    public  TextureUsage        usage;
    public  TextureDimension    dimension;
    public  TextureSize         size;
    public  TextureFormat       format;
    public  int                 mipLevelCount   = 1;
    public  int                 sampleCount     = 1;
    public  TextureFormat[]     viewFormats;

    public GpuTextureDescriptor() { }
}

public sealed unsafe partial class WgpuDevice
{
    private static void SetTextureDescriptor(ref TextureDescriptor native, in GpuTextureDescriptor descriptor)
    {
        native.dimension                  = descriptor.dimension;
        native.size.width                 = (uint)descriptor.size.width;
        native.size.height                = (uint)descriptor.size.height;
        native.size.depthOrArrayLayers    = (uint)descriptor.size.depthOrArrayLayers;
        native.format                     = descriptor.format;
        native.usage                      = (ulong)descriptor.usage;
        native.sampleCount                = (uint)descriptor.sampleCount;
        native.mipLevelCount              = (uint)descriptor.mipLevelCount;
        native.nextInChain                = (ChainedStruct*)descriptor.nextInChain;
        native.viewFormatCount            = (uint)(descriptor.viewFormats?.Length ?? 0);
    }

    public GpuTexture CreateTexture(in GpuTextureDescriptor? descriptor = null)
    {
        var desc    = new TextureDescriptor();
        var src     = descriptor ?? new GpuTextureDescriptor();
        SetTextureDescriptor(ref desc, src);
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(src.label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        desc.label              = WgpuUtils.CopyToStringView(src.label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = src.viewFormats) {
            desc.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
            return new GpuTexture(this, src, texture);
        }
    }
}