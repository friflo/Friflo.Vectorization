// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.ComponentModel;

// file contains structs created by:  CommandRecorder

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.Runtime;

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
}
