// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Friflo.Vectorization.GPU;

public class GpuBindGroupLayoutBuilder
{
    private readonly GpuContext Context;
    
    private readonly List<BindGroupLayoutEntry> _entries = new();
    
    internal GpuBindGroupLayoutBuilder(GpuContext ctx) {
        Context = ctx;
    }
    
    private void AddLayoutEntry(int binding, BufferBindingType bindingType)
    {
        _entries.Add(new BindGroupLayoutEntry {
            Binding = (uint)binding,
            Visibility = ShaderStage.Compute,       // <--- we do compute
            Buffer = new BufferBindingLayout {
                Type                = bindingType,
                HasDynamicOffset    = false,        // default
                MinBindingSize      = 0             // 0: no validation of minimum size
            }
        });
    }
    
    public GpuBindGroupLayoutBuilder AddBuffer<T>(int binding, string name) where T : unmanaged
    {
        AddLayoutEntry (binding, BufferBindingType.Storage);
        return this;
    }
    
    public GpuBindGroupLayoutBuilder AddReadOnlyBuffer<T>(int binding, string name) where T : unmanaged
    {
        AddLayoutEntry (binding, BufferBindingType.ReadOnlyStorage);
        return this;
    }

    public GpuBindGroupLayoutBuilder AddUniform<T>(int binding, string name) where T : unmanaged
    {
        AddLayoutEntry (binding, BufferBindingType.Uniform);
        return this;
    }

    public unsafe GpuBindGroupLayout Build(ReadOnlySpan<byte> label)
    {
        fixed (byte* labelPtr = label)
        fixed (BindGroupLayoutEntry* pEntries = _entries.ToArray())
        {
            var desc = new BindGroupLayoutDescriptor {
                Label       = labelPtr,
                EntryCount  = (uint)_entries.Count,
                Entries     = pEntries,
            };

            var handle = Context._wgpu.DeviceCreateBindGroupLayout(Context.DevicePtr, &desc);
            
            if (handle == null)
                throw new Exception("Failed to create BindGroupLayout. Check your Slot-indexes!");

            return new GpuBindGroupLayout(Context, handle);
        }
    }
}

public unsafe class GpuBindGroupLayout
{
    internal GpuContext         Context;
    internal BindGroupLayout*   Handle;
    
    internal GpuBindGroupLayout (GpuContext context, BindGroupLayout* handle)
    {
        Context = context;
        Handle  = handle;
    }
}

public unsafe struct GpuBindEntry
{
    public uint     Binding;
    public Buffer*  BufferHandle; // or Type: IGpuBuffer interface that shares oll GpuBuffer<T>'s
    public uint     Offset;
    public uint     Size;
    
    public static GpuBindEntry From<T>(int binding, GpuBuffer<T> buffer) where T : unmanaged {
        return new GpuBindEntry(binding, buffer._handle, 0, (uint)(Unsafe.SizeOf<T>() * buffer.Length));
    }

    public GpuBindEntry(int binding, GpuBuffer<byte> pool, uint offset, uint size) 
        : this(binding, pool._handle, offset, size) { }

    private GpuBindEntry(int binding, Buffer* handle, uint offset, uint size) {
        Binding         = (uint)binding;
        BufferHandle    = handle;
        Offset          = offset;
        Size            = size;
    }
}
