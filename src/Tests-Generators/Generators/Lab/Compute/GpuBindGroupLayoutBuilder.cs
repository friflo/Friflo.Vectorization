// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Friflo.Vectorization.GPU;

public sealed class GpuBindGroupLayoutBuilder
{
    private readonly GpuContext                 context;
    private readonly List<BindGroupLayoutEntry> entries = new();
    
    internal GpuBindGroupLayoutBuilder(GpuContext ctx) {
        context = ctx;
    }
    
    private void AddLayoutEntry(int binding, BufferBindingType bindingType)
    {
        entries.Add(new BindGroupLayoutEntry {
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
        fixed (BindGroupLayoutEntry* pEntries = entries.ToArray())
        {
            var desc = new BindGroupLayoutDescriptor {
                Label       = labelPtr,
                EntryCount  = (uint)entries.Count,
                Entries     = pEntries,
            };

            var handle = context.wgpu.DeviceCreateBindGroupLayout(context.DevicePtr, &desc);
            
            if (handle == null)
                throw new Exception("Failed to create BindGroupLayout. Check your Slot-indexes!");

            return new GpuBindGroupLayout(context, handle);
        }
    }
}

public sealed unsafe class GpuBindGroupLayout
{
    internal readonly GpuContext        context;
    internal readonly BindGroupLayout*  handle;
    
    internal GpuBindGroupLayout (GpuContext context, BindGroupLayout* handle)
    {
        this.context = context;
        this.handle  = handle;
    }
}

public unsafe struct GpuBindEntry
{
    public readonly uint    binding;
    public readonly Buffer* bufferHandle; // or Type: IGpuBuffer interface that shares oll GpuBuffer<T>'s
    public readonly uint    offset;
    public readonly uint    size;
    
    public static GpuBindEntry From<T>(int binding, GpuBuffer<T> buffer) where T : unmanaged {
        return new GpuBindEntry(binding, buffer.handle, 0, (uint)(Unsafe.SizeOf<T>() * buffer.Length));
    }

    public GpuBindEntry(int binding, GpuBuffer<byte> pool, uint offset, uint size) 
        : this(binding, pool.handle, offset, size) { }

    private GpuBindEntry(int binding, Buffer* handle, uint offset, uint size) {
        this.binding         = (uint)binding;
        bufferHandle    = handle;
        this.offset          = offset;
        this.size            = size;
    }
}
