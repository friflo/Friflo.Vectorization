// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


public struct TextureSize
{
    public  int width;
    public  int height;
    public  int depthOrArrayLayers = 1;
    
    public TextureSize() { }
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