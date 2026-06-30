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
    public  int     height              = 1;
    public  int     depthOrArrayLayers  = 1;
    
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
    public  TextureDimension    dimension       = TextureDimension.D2D;
    public  TextureSize         size;
    public  TextureFormat       format;
    public  int                 mipLevelCount   = 1;
    public  int                 sampleCount     = 1;
    public  TextureFormat[]     viewFormats;

    public GpuTextureDescriptor() { }
}

public sealed unsafe partial class WgpuDevice
{
    /// <remarks>
    /// <remarks>Same behavior as: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPUDevice/createTexture">MDN: GPUDevice.createTexture()</a></remarks>
    /// </remarks>
    public GpuTexture CreateTexture(in GpuTextureDescriptor? descriptor = null)
    {
        var native  = new TextureDescriptor();
        var src     = descriptor ?? new GpuTextureDescriptor();
        native.nextInChain              = (ChainedStruct*)src.nextInChain;
        native.usage                    = (ulong)src.usage;
        native.dimension                = src.dimension;
        native.size.width               = (uint)src.size.width;
        native.size.height              = (uint)src.size.height;
        native.size.depthOrArrayLayers  = (uint)src.size.depthOrArrayLayers;
        native.format                   = src.format;
        native.mipLevelCount            = (uint)src.mipLevelCount;
        native.sampleCount              = (uint)src.sampleCount;
        native.viewFormatCount          = (uint)(src.viewFormats?.Length ?? 0);
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(src.label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        native.label              = WgpuUtils.CopyToStringView(src.label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = src.viewFormats) {
            native.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &native);
            return new GpuTexture(this, src, texture);
        }
    }
}