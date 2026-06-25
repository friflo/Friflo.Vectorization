// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
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
        int                     viewFormatCount)
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
        desc.viewFormatCount            = (uint)viewFormatCount;
    }

    /// <summary> Mimics <c>createTexture()</c> from WebGPU JavaScript - same as <see cref="CreateTexture2D"/>  </summary>
    public GpuTexture2D CreateTexture(int width, int height, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        return CreateTexture2D(width, height, format, usage, label, in options, viewFormats);
    }
}