// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


/// <summary> <see cref="TextureDescriptor"/> </summary>
public struct TextureOptions
{
    public  unsafe  ChainedStruct*  nextInChain;
    public          uint            mipLevelCount       = 1;
    public          uint            sampleCount         = 1;
    public          uint            depthOrArrayLayers  = 1;

    public TextureOptions() { }
}

public sealed unsafe partial class WgpuDevice
{
    private static void SetTextureDescriptor(
        ref TextureDescriptor   desc,
        TextureDimension        dimension,
        int                     width,
        int                     height,
        TextureFormat           format,
        TextureUsage            usage,
        in TextureOptions?      options,
        Span<TextureFormat>     viewFormats)
    {
        var opt = options ?? new TextureOptions();
        
        desc.dimension                  = dimension;
        desc.size.width                 = (uint)width;
        desc.size.height                = (uint)height;
        desc.format                     = format;
        desc.usage                      = (ulong)usage;
        desc.size.depthOrArrayLayers    = opt.depthOrArrayLayers;
        desc.sampleCount                = opt.sampleCount;
        desc.mipLevelCount              = opt.mipLevelCount;
        desc.nextInChain                = opt.nextInChain;
        desc.viewFormatCount            = (uint)viewFormats.Length;
    }

    /// <summary> Mimics <c>createTexture()</c> from WebGPU JavaScript - same as <see cref="CreateTexture2D"/>  </summary>
    public GpuTexture2D CreateTexture(int width, int height, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        return CreateTexture2D(width, height, format, usage, label, in options, viewFormats);
    }
    
    public GpuTexture2D CreateTexture2D(int width, int height, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        var desc = new TextureDescriptor();
        SetTextureDescriptor(ref desc, TextureDimension.D2D, width, height, format, usage, in options, viewFormats);
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        desc.label              = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = viewFormats) {
            desc.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
            return new GpuTexture2D(this, desc, texture);
        }
    }
}