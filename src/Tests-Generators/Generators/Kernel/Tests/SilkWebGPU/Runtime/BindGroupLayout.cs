// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;

// ReSharper disable once CheckNamespace
namespace Kernel.SilkWebGPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
[StructLayout(LayoutKind.Sequential)]
public readonly unsafe struct SilkBindGroupLayout
{
    internal readonly   BindGroupLayout*    handle;         // must contain only this single file
    public              bool                IsCreated =>    handle != null;
    
    public override     string              ToString()  => handle != null ? "Created" : "null";
    
    internal SilkBindGroupLayout (BindGroupLayout* handle) {
        this.handle = handle;
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
[StructLayout(LayoutKind.Sequential)]
public readonly struct SilkLayoutEntry
{
    internal readonly   int                 Binding;
    internal readonly   BufferBindingType   Type;

    public override string ToString() => $"{Binding} {Type}";

    private SilkLayoutEntry(int binding, BufferBindingType type) {
        Binding = binding;
        Type    = type;
    }
    
    public static SilkLayoutEntry Uniform<T>(int binding)            => new (binding,    BufferBindingType.Uniform);
    public static SilkLayoutEntry ReadWriteStorage<T>(int binding)   => new (binding,    BufferBindingType.Storage);
    public static SilkLayoutEntry ReadOnlyStorage<T>(int binding)    => new (binding,    BufferBindingType.ReadOnlyStorage);
}