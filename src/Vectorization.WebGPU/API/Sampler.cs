// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ReplaceWithFieldKeyword
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


public sealed unsafe partial class WgpuDevice
{
    public FilteringSampler CreateFilteringSampler(
        FilterMode          magFilter       = FilterMode.Linear,
        FilterMode          minFilter       = FilterMode.Linear,
        MipmapFilterMode    mipmapFilter    = MipmapFilterMode.Linear,
        ushort              maxAnisotropy   = 1,
        string              label           = null,
        in SamplerOptions?  options         = null)
    {
        var desc = new SamplerDescriptor {
            magFilter       = magFilter,
            minFilter       = minFilter,
            mipmapFilter    = mipmapFilter,
            maxAnisotropy   = maxAnisotropy
        };
        desc = CreateSampler(desc, label, options, out var sampler);
        return new FilteringSampler(sampler, desc, label);
    }
    
    public NonFilteringSampler CreateNonFilteringSampler(
        string              label       = null,
        in SamplerOptions?  options     = null)
    {
        var desc = new SamplerDescriptor {
            magFilter       = FilterMode.Nearest,
            minFilter       = FilterMode.Nearest,
            mipmapFilter    = MipmapFilterMode.Nearest,
            maxAnisotropy   = 1,
        };
        desc = CreateSampler(desc, label, options, out var sampler);
        return new NonFilteringSampler(sampler, desc, label);
    }
    
    public ComparisonSampler CreateComparisonSampler(
        CompareFunction     compare,
        FilterMode          magFilter       = FilterMode.Linear,
        FilterMode          minFilter       = FilterMode.Linear,
        MipmapFilterMode    mipmapFilter    = MipmapFilterMode.Nearest,
        string              label           = null,
        in SamplerOptions?  options         = null)
    {
        var desc = new SamplerDescriptor {
            compare         = compare,
            magFilter       = magFilter,
            minFilter       = minFilter,
            mipmapFilter    = mipmapFilter,
            maxAnisotropy   = 1
        };
        desc = CreateSampler(desc, label, options, out var sampler);
        return new ComparisonSampler(sampler, desc, label);
    }
        
    private SamplerDescriptor CreateSampler(
        SamplerDescriptor   desc,
        string              label,
        in SamplerOptions?  options,
        out Sampler*        sampler)
    {
        var opt = options ?? new SamplerOptions();
        desc.nextInChain     = opt.nextInChain;
        desc.addressModeU    = opt.addressModeU;
        desc.addressModeV    = opt.addressModeV;
        desc.addressModeW    = opt.addressModeW;
        desc.lodMinClamp     = opt.lodMinClamp;
        desc.lodMaxClamp     = opt.lodMaxClamp;

        int labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte* labelBuffer   = stackalloc byte[labelMaxCount];
        desc.label          = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);

        sampler = wgpuDeviceCreateSampler(DevicePtr, &desc);
        
        desc.label = default;
        return desc;
    }
}

public struct SamplerOptions
{
    public unsafe   ChainedStruct*  nextInChain;
    public          AddressMode     addressModeU    = AddressMode.ClampToEdge;
    public          AddressMode     addressModeV    = AddressMode.ClampToEdge;
    public          AddressMode     addressModeW    = AddressMode.ClampToEdge;
    public          float           lodMinClamp     = 0.0f;
    public          float           lodMaxClamp     = 32.0f;

    public SamplerOptions() { }
}


public abstract unsafe class GpuSampler : IDisposable
{
    internal            Sampler*            handle;
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

/// <summary> Maps to  WGSL type: <c>sampler</c>. </summary>
/// <remarks> <see cref="SamplerBindingLayout"/> is created with <see cref="SamplerBindingType.Filtering"/>. </remarks>
public sealed unsafe class FilteringSampler : GpuSampler
{
    public FilteringSampler(Sampler* handle, in SamplerDescriptor desc, string label) : base(handle, in desc, label) { }
}

/// <summary> Maps to  WGSL type: <c>sampler</c>. </summary>
/// <remarks> <see cref="SamplerBindingLayout"/> is created with <see cref="SamplerBindingType.NonFiltering"/>. </remarks>
public sealed unsafe class NonFilteringSampler : GpuSampler
{
    public NonFilteringSampler(Sampler* handle, in SamplerDescriptor desc, string label) : base(handle, in desc, label) { }
}

/// <summary> Maps to  WGSL type: <c>sampler_comparison</c>. </summary>
/// <remarks> <see cref="SamplerBindingLayout"/> is created with <see cref="SamplerBindingType.Comparison"/>. </remarks>
public sealed unsafe class ComparisonSampler : GpuSampler
{
    public ComparisonSampler(Sampler* handle, in SamplerDescriptor desc, string label) : base(handle, in desc, label) { }
}



/// <summary>
/// Classes extending <see cref="GpuSampler"/> define the <see cref="BindGroupLayoutEntry.sampler"/>
/// </summary>
/// <remarks>
/// Bind group layout creation:<br/>
/// <see cref="BindGroupLayoutEntry"/>'s are used to create a <see cref="BindGroupLayoutDescriptor"/>.<br/>
/// The descriptor is used to create a <see cref="BindGroupLayout"/> handle with <see cref="wgpuDeviceCreateBindGroupLayout"/>.<br/>
/// <br/>
/// Bind group creation:<br/>
/// The <see cref="BindGroupLayout"/> handle is used in <see cref="BindGroupDescriptor.entries"/> to create a <see cref="BindGroup"/> handle.<br/>
/// These <see cref="BindGroupDescriptor.entries"/> are of type <see cref="BindGroupEntry"/>.<br/> 
/// A <see cref="BindGroupEntry.sampler"/> can be assigned with <see cref="GpuSampler.handle"/><br/>
/// <br/>
/// Important for understanding:<br/>
/// A <see cref="Sampler"/>* defines an immutable configuration state created with <see cref="wgpuDeviceCreateSampler"/>.<br/>
/// <br/>
/// <see cref="SamplerBindingLayout"/> fields used in <see cref="BindGroupLayoutEntry.sampler"/>:<br/>
/// - <see cref="SamplerBindingLayout.type"/><br/>
/// </remarks>
internal interface ISamplerDocs { }

