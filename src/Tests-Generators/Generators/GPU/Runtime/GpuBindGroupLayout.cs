// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;

namespace Friflo.Vectorization.GPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
[StructLayout(LayoutKind.Sequential)]
public readonly unsafe struct GpuBindGroupLayout
{
    internal readonly   BindGroupLayout*    handle;     // must contain only this single file
    
    public override     string              ToString()  => handle != null ? "Created" : "null";
    
    internal GpuBindGroupLayout (BindGroupLayout* handle) {
        this.handle = handle;
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
[StructLayout(LayoutKind.Sequential)]
public readonly struct GpuLayoutEntry
{
    internal readonly   int                 Binding;
    internal readonly   BufferBindingType   Type;

    public override string ToString() => $"{Binding} {Type}";

    private GpuLayoutEntry(int binding, BufferBindingType type) {
        Binding = binding;
        Type    = type;
    }
    
    public static GpuLayoutEntry Uniform<T>(int binding)            => new (binding,    BufferBindingType.Uniform);
    public static GpuLayoutEntry ReadWriteStorage<T>(int binding)   => new (binding,    BufferBindingType.Storage);
    public static GpuLayoutEntry ReadOnlyStorage<T>(int binding)    => new (binding,    BufferBindingType.ReadOnlyStorage);
}