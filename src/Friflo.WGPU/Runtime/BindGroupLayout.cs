// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.Runtime;

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
