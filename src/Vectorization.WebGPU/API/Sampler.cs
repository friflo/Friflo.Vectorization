// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ReplaceWithFieldKeyword
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


public static unsafe partial class WgpuExtensions
{
    public static GpuSampler CreateSampler(this GpuDevice device, in GpuSamplerDescriptor descriptor)
    {
        var desc = new SamplerDescriptor {
            nextInChain     = (ChainedStruct*)descriptor.nextInChain,
            addressModeU    = descriptor.addressModeU,
            addressModeV    = descriptor.addressModeV,
            addressModeW    = descriptor.addressModeW,
            magFilter       = descriptor.magFilter,
            minFilter       = descriptor.minFilter,
            mipmapFilter    = descriptor.mipmapFilter,
            lodMinClamp     = descriptor.lodMinClamp,
            lodMaxClamp     = descriptor.lodMaxClamp,
            compare         = descriptor.compare,
            maxAnisotropy   = descriptor.maxAnisotropy,
        };
        int labelMaxCount   = WgpuUtils.GetMaxCount(descriptor.label);
        byte* labelBuffer   = stackalloc byte[labelMaxCount];
        desc.label          = WgpuUtils.CopyToStringView(descriptor.label, labelBuffer, labelMaxCount);
        var wgpuDevice = (WgpuDevice)device;
        var sampler = wgpuDeviceCreateSampler(wgpuDevice.DevicePtr, &desc);
        
        return new GpuSampler(sampler, descriptor);
    }
}

/// <summary> Managed type for:  <see cref="SamplerDescriptor"/> </summary>
/// <remarks>Default values see: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPUDevice/createSampler">MDN: GPUDevice.createSampler()</a></remarks>
public struct GpuSamplerDescriptor
{
    public  nint                nextInChain;
    public  string              label;
    public  AddressMode         addressModeU    = AddressMode.ClampToEdge;
    public  AddressMode         addressModeV    = AddressMode.ClampToEdge;
    public  AddressMode         addressModeW    = AddressMode.ClampToEdge;
    public  FilterMode          magFilter       = FilterMode.Nearest;
    public  FilterMode          minFilter       = FilterMode.Nearest;
    public  MipmapFilterMode    mipmapFilter    = MipmapFilterMode.Nearest;
    public  float               lodMinClamp     = 0.0f;
    public  float               lodMaxClamp     = 32.0f;
    public  CompareFunction     compare;
    public  ushort              maxAnisotropy   = 1;

    public GpuSamplerDescriptor() { }
}

/// <summary>
/// When used as a shader method parameter the parameter must have a <see cref="SamplerTypeAttribute"/>.
/// </summary>
public sealed unsafe class GpuSampler : IDisposable
{
    internal            Sampler*                handle;
    private readonly    GpuSamplerDescriptor    desc;
    public              string                  Label       => desc.label;
    public              nint                    Handle      => (nint)handle;
    public ref readonly GpuSamplerDescriptor    Descriptor  => ref desc;
    public              bool                    IsDisposed  => handle == null;
    public  override    string                  ToString()  => Label;
    
    internal GpuSampler(Sampler* handle, in GpuSamplerDescriptor descriptor) {
        this.handle = handle;
        desc        = descriptor;
    }
    
    public void Dispose()
    {
        if (handle != null) {
            wgpuSamplerRelease(handle);
            handle = null;
        }
    }
}


