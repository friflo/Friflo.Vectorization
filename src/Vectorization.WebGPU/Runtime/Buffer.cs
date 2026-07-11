// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;


internal interface IWgpuBuffer {
    internal ref readonly BufferData    GetBufferData();
    internal Span<byte>                 GetHostMemorySpan();
}

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed unsafe class WgpuBuffer<T> : GpuBuffer<T>, IWgpuBuffer where T : unmanaged
{
    internal            Buffer*     handle { get; private set; }
    private             WgpuDevice  device { get; set; }
    private  readonly   BufferData  data;
    
    // --- GpuBuffer
    public    override  GpuDevice   Device      => device;
    public    override  bool        IsDisposed  => handle == null;
    
    // --- IWgpuBuffer
    ref readonly BufferData IWgpuBuffer.GetBufferData()     => ref data;
    Span<byte>              IWgpuBuffer.GetHostMemorySpan() => MemoryMarshal.Cast<T, byte>(hostMemory.Span);
    
    // Every class implementing IDispose must follow the same pattern. Set GpuInstance code sample.
    public override void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
   ~WgpuBuffer() {
        Dispose(false);  // false: release only native pointers.
    }
    
    private void Dispose(bool _)
    {
        if (handle == null) return;
        wgpuBufferRelease(handle);
        handle = null;
        device = null;
    }

    internal WgpuBuffer(WgpuDevice device, Buffer* buffer, int bufferId, Memory<T> hostMemory, string bufferLabel)
        : base(hostMemory, bufferLabel, (nint)buffer, bufferId)
    {
        this.device     = device;
        handle          = buffer;
        data            = new BufferData(bufferId, Unsafe.SizeOf<T>(), buffer, bufferLabel);
    }
}

