// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Friflo.Vectorization.GPU;

public readonly unsafe struct GpuBindGroupLayout
{
    internal readonly BindGroupLayout*  handle;
    
    internal GpuBindGroupLayout (BindGroupLayout* handle) {
        this.handle = handle;
    }
}

public readonly unsafe struct GpuBindEntry
{
    public readonly uint    binding;
    public readonly Buffer* bufferHandle; // or Type: IGpuBuffer interface that shares oll GpuBuffer<T>'s
    public readonly uint    offset;
    public readonly uint    size;
    
    public static GpuBindEntry From<T>(int binding, GpuBuffer<T> buffer) where T : unmanaged {
        return new GpuBindEntry(binding, buffer.handle, 0, (uint)(Unsafe.SizeOf<T>() * buffer.Length));
    }

    internal GpuBindEntry(int binding, GpuBuffer<byte> pool, uint offset, uint size) 
        : this(binding, pool.handle, offset, size) { }

    private GpuBindEntry(int binding, Buffer* handle, uint offset, uint size) {
        this.binding         = (uint)binding;
        bufferHandle    = handle;
        this.offset          = offset;
        this.size            = size;
    }
}

internal enum GpuBindingType {
    Uniform,
    Storage,
    ReadOnlyStorage,
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct GpuLayoutEntry
{
    public   readonly   int                 Binding;
    internal readonly   GpuBindingType      Type;

    public override string ToString() => $"{Binding} {Type}";

    private GpuLayoutEntry(int binding, GpuBindingType readOnlyStorage) {
        Binding = binding;
        Type    = readOnlyStorage;
    }
    
    public static           GpuLayoutEntry Uniform<T>(int binding) 
        => new (binding,    GpuBindingType.Uniform);

    public static           GpuLayoutEntry ReadWriteStorage<T>(int binding) 
        => new (binding,    GpuBindingType.Storage);
    
    public static           GpuLayoutEntry ReadOnlyStorage<T>(int binding)
        => new (binding,    GpuBindingType.ReadOnlyStorage);
}