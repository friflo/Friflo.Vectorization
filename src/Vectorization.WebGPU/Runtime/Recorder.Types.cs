// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;

// file contains structs created by:  CommandRecorder

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
internal readonly unsafe struct WgpuEncoder
{
    internal readonly   CommandEncoder* handle;
    
    public   override   string          ToString() => handle != null ? "Created" : "null";
    
    internal WgpuEncoder(CommandEncoder* handle) {
        this.handle = handle;
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
internal readonly unsafe struct WgpuCommandBuffer
{
    internal readonly   CommandBuffer*  handle;
    
    public   override   string          ToString() => handle != null ? "Created" : "null";
    
    internal WgpuCommandBuffer(CommandBuffer* handle) {
        this.handle = handle;
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct WgpuBindGroup
{
    internal readonly   BindGroup*  handle;
    public              bool        IsCreated => handle != null;
    
    public   override   string      ToString() => handle != null ? "Created" : "null";
    
    internal WgpuBindGroup(BindGroup* handle) {
        this.handle = handle;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BindGroupEntry From<T>(int binding, GpuBuffer<T> buffer) where T : unmanaged
    {
        return new BindGroupEntry {
            binding = (uint)binding,
            buffer  = (Buffer*)buffer.NativeHandle,
            offset  = 0,
            size    = (uint)(Unsafe.SizeOf<T>() * buffer.Length)
        };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BindGroupEntry From(int binding, GpuSampler sampler)
    {
        return new BindGroupEntry {
            binding = (uint)binding,
            sampler = sampler.handle,
        };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BindGroupEntry From<T>(int binding, T textureView) where T : ITextureView
    {
        return new BindGroupEntry {
            binding = (uint)binding,
            textureView = (TextureView*)textureView.Handle
        };
    }
}
