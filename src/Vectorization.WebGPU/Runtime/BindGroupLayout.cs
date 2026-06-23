// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
[StructLayout(LayoutKind.Sequential)]
public readonly unsafe struct WgpuBindGroupLayout
{
    internal readonly   BindGroupLayout*    handle;         // must contain only this single file
    public              bool                IsCreated =>    handle != null;
    
    public override     string              ToString()  => handle != null ? "Created" : "null";
    
    internal WgpuBindGroupLayout (BindGroupLayout* handle) {
        this.handle = handle;
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
[StructLayout(LayoutKind.Sequential)]
public readonly struct WgpuLayoutEntry
{
    internal readonly   LayoutEntryType         type;
    internal readonly   int                     Binding;
    internal readonly   BufferBindingType       BufferType;     // Buffer / Uniform
    internal readonly   SamplerBindingType      samplerType;    // Sampler
    internal readonly   TextureSampleType       sampleType;     // Texture
    internal readonly   TextureViewDimension    viewDimension;  // Texture
    internal readonly   bool                    multisampled;   // Texture

    public override string ToString() => $"{Binding} {BufferType}";

    private WgpuLayoutEntry(int binding, LayoutEntryType type, BufferBindingType bufferType) {
        this.type   = type;
        Binding     = binding;
        BufferType  = bufferType;
    }
    
    private WgpuLayoutEntry(int binding, SamplerBindingType samplerType) {
        type                = LayoutEntryType.Sampler;
        Binding             = binding;
        this.samplerType    = samplerType;
    }
    
    private WgpuLayoutEntry(int binding, TextureSampleType sampleType, TextureViewDimension viewDimension, bool multisampled) {
        type                = LayoutEntryType.Texture;
        Binding             = binding;
        this.sampleType     = sampleType;
        this.viewDimension  = viewDimension;
        this.multisampled   = multisampled;
    }
    
    public static WgpuLayoutEntry Uniform         (int binding) => new (binding, LayoutEntryType.Uniform, BufferBindingType.Uniform);
    public static WgpuLayoutEntry ReadWriteStorage(int binding) => new (binding, LayoutEntryType.Buffer,  BufferBindingType.Storage);
    public static WgpuLayoutEntry ReadOnlyStorage (int binding) => new (binding, LayoutEntryType.Buffer,  BufferBindingType.ReadOnlyStorage);
    
    public static WgpuLayoutEntry Sampler         (int binding, SamplerBindingType samplerType) => new (binding, samplerType);
    public static WgpuLayoutEntry Texture         (int binding, TextureSampleType sampleType, TextureViewDimension viewDimension, bool multisampled)
                                                    => new(binding, sampleType, viewDimension, multisampled);
}

internal enum LayoutEntryType
{
    Buffer,
    Uniform,
    Sampler,
    Texture,
}