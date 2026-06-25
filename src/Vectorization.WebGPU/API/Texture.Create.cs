// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


public sealed unsafe partial class WgpuDevice
{
    public GpuTexture2D CreateTexture2D(int width, int height, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        var desc = new TextureDescriptor();
        SetTextureDescriptor(ref desc, TextureDimension.D2D, width, height, format, usage, in options, viewFormats.Length);
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        desc.label              = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = viewFormats) {
            desc.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
            return new GpuTexture2D(this, desc, texture, label);
        }
    }
}