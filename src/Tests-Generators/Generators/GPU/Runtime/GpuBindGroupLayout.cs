// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;

namespace Friflo.Vectorization.GPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe struct GpuBindGroupLayout
{
    internal readonly BindGroupLayout*  handle;
    
    internal GpuBindGroupLayout (BindGroupLayout* handle) {
        this.handle = handle;
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
internal enum GpuBindingType {
    Uniform,
    Storage,
    ReadOnlyStorage,
}

[EditorBrowsable(EditorBrowsableState.Never)]
[StructLayout(LayoutKind.Sequential)]
public readonly struct GpuLayoutEntry
{
    internal readonly   int                 Binding;
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