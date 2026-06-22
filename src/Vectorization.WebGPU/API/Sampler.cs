// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable ReplaceWithFieldKeyword
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable UnassignedField.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


public sealed unsafe partial class WgpuDevice
{
    public GpuSampler CreateSampler(
        FilterMode magFilter       = FilterMode.Nearest,
        FilterMode minFilter       = FilterMode.Nearest,
        string label               = null,
        in SamplerOptions? options = null)
    {
        var opt = options ?? new SamplerOptions();
        var desc = new SamplerDescriptor
        {
            nextInChain     = opt.nextInChain,
            magFilter       = magFilter,
            minFilter       = minFilter,
            addressModeU    = opt.addressModeU,
            addressModeV    = opt.addressModeV,
            addressModeW    = opt.addressModeW,
            mipmapFilter    = opt.mipmapFilter,
            lodMinClamp     = opt.lodMinClamp,
            lodMaxClamp     = opt.lodMaxClamp,
            compare         = opt.compare,
            maxAnisotropy   = opt.maxAnisotropy
        };
        int labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte* labelBuffer   = stackalloc byte[labelMaxCount];
        desc.label          = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);

        Sampler* sampler = wgpuDeviceCreateSampler(DevicePtr, &desc);
        
        desc.label = default;
        return new GpuSampler(sampler, desc, label);
    }
}

public struct SamplerOptions
{
    public unsafe   ChainedStruct*      nextInChain;
    public          AddressMode         addressModeU   = AddressMode.ClampToEdge;
    public          AddressMode         addressModeV   = AddressMode.ClampToEdge;
    public          AddressMode         addressModeW   = AddressMode.ClampToEdge;
    public          MipmapFilterMode    mipmapFilter   = MipmapFilterMode.Linear;
    public          float               lodMinClamp    = 0.0f;
    public          float               lodMaxClamp    = float.MaxValue;
    public          CompareFunction     compare        = CompareFunction.Undefined;
    public          ushort              maxAnisotropy  = 1;

    public SamplerOptions() { }
}

public sealed unsafe class GpuSampler : IDisposable
{
    private             Sampler*            handle;
    private readonly    SamplerDescriptor   desc;
    public  readonly    string              Label;
    
    public ref readonly SamplerDescriptor   Descriptor  => ref desc;
    public              bool                IsDisposed  => handle == null;
    public  override    string              ToString()  => Label;
    
    internal GpuSampler(Sampler* handle, in SamplerDescriptor desc, string label) {
        this.handle = handle;
        this.desc   = desc;
        Label       = label;
    }
    
    public void Dispose()
    {
        if (handle != null) {
            wgpuSamplerRelease(handle);
            handle = null;
        }
    }
}
