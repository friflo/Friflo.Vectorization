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

#pragma warning disable CS8981 // typename: sampler:  The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

public sealed unsafe partial class WgpuDevice
{
    public sampler CreateSampler(
        SamplerType         samplerType = SamplerType.Filtering,
        FilterMode          magFilter   = FilterMode.Nearest,
        FilterMode          minFilter   = FilterMode.Nearest,
        string              label       = null,
        in SamplerOptions?  options     = null)
    {
        var opt     = options ?? new SamplerOptions();
        var desc    = new SamplerDescriptor();
        opt.SetSamplerDescriptor(ref desc);
        desc.magFilter       = magFilter;
        desc.minFilter       = minFilter;

        int labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte* labelBuffer   = stackalloc byte[labelMaxCount];
        desc.label          = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);

        Sampler* sampler = wgpuDeviceCreateSampler(DevicePtr, &desc);
        
        desc.label = default;
        return new sampler(sampler, desc, samplerType, label);
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
    
    internal unsafe void SetSamplerDescriptor(ref SamplerDescriptor desc)
    {
        desc.nextInChain     = nextInChain;
        desc.addressModeU    = addressModeU;
        desc.addressModeV    = addressModeV;
        desc.addressModeW    = addressModeW;
        desc.mipmapFilter    = mipmapFilter;
        desc.lodMinClamp     = lodMinClamp;
        desc.lodMaxClamp     = lodMaxClamp;
        desc.compare         = compare;
        desc.maxAnisotropy   = maxAnisotropy;
    }
}

public enum SamplerType {
    Filtering,
    NonFiltering
}

public sealed unsafe class sampler : IDisposable
{
    private             Sampler*            handle;
    private readonly    SamplerDescriptor   desc;
    public  readonly    string              Label;
    public  readonly    SamplerType         SamplerType;
    
    public ref readonly SamplerDescriptor   Descriptor  => ref desc;
    public              bool                IsDisposed  => handle == null;
    public  override    string              ToString()  => Label;
    
    internal sampler(Sampler* handle, in SamplerDescriptor desc, SamplerType type, string label) {
        this.handle = handle;
        this.desc   = desc;
        Label       = label;
        SamplerType = type;
    }
    
    public void Dispose()
    {
        if (handle != null) {
            wgpuSamplerRelease(handle);
            handle = null;
        }
    }
}


public readonly unsafe struct SamplerHandle  // TODO may be removed
{
    private  readonly   Sampler*    handle;
}

/// <summary>
/// Names of struct types implementing <see cref="ISampler"/> define the <see cref="BindGroupLayoutEntry.sampler"/>
/// </summary>
/// <remarks>
/// Bind group layout creation:<br/>
/// <see cref="BindGroupLayoutEntry"/>'s are used to create a <see cref="BindGroupLayoutDescriptor"/>.<br/>
/// The descriptor is used to create a <see cref="BindGroupLayout"/> handle with <see cref="wgpuDeviceCreateBindGroupLayout"/>.<br/>
/// <br/>
/// Bind group creation:<br/>
/// The <see cref="BindGroupLayout"/> handle is used in <see cref="BindGroupDescriptor.entries"/> to create a <see cref="BindGroup"/> handle.<br/>
/// These <see cref="BindGroupDescriptor.entries"/> are of type <see cref="BindGroupEntry"/>.<br/> 
/// A <see cref="BindGroupEntry.sampler"/> can be assigned with <see cref="SamplerHandle.handle"/><br/>
/// <br/>
/// Important for understanding:<br/>
/// A <see cref="Sampler"/>* defines an immutable configuration state created with <see cref="wgpuDeviceCreateSampler"/>.<br/>
/// <br/>
/// <see cref="SamplerBindingLayout"/> fields used in <see cref="BindGroupLayoutEntry.sampler"/>:<br/>
/// - <see cref="SamplerBindingLayout.type"/><br/>
/// </remarks>
public interface ISampler
{
    SamplerHandle Handle { get; }
}



#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
