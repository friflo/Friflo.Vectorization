// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;


// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;

public sealed unsafe partial class WgpuDevice
{
    public GpuTexture2D CreateTexture2D(int width, int height, TextureDescriptor desc)
    {
        desc.size.width                 = (uint)width;
        desc.size.height                = (uint)height;
        desc.size.depthOrArrayLayers    = 1;  // always 1 for 2D
        if (desc.sampleCount == 0) {
            desc.sampleCount = 1;
        }
        if (desc.mipLevelCount == 0) {
            desc.mipLevelCount = 1;
        }
        Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
        return new GpuTexture2D(this, desc, texture);
    }
}