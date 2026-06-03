// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;
using Buffer = Friflo.Vectorization.WebGPU.Runtime.Buffer;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable InlineTemporaryVariable
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU;


internal unsafe interface IWgpuBuffer {
    internal    ref BufferData  GetBufferData();
    internal    void            ExecuteCpuCopy(void* pMapped, List<BufferRange> bufferRanges);
}

public sealed unsafe class WgpuBuffer<T> : GpuBuffer<T>, IWgpuBuffer where T : unmanaged
{
    internal            Buffer*     handle { get; private set; }
    private             WgpuDevice  device { get; set; }
    private  readonly   uint        SizeInBytes;    
    private             BufferData  data;
    // --- GpuBuffer
    public    override  GpuDevice   Device      => device;
    public    override  bool        IsDisposed  => handle == null;
    
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
        wgpuBufferRelease(data.stagingHandle);
        handle = null;
        device = null;
    }

    internal WgpuBuffer(WgpuDevice device, Buffer* buffer, int bufferId, Buffer* statingHandle, Memory<T> hostMemory, string bufferLabel)
        : base(hostMemory, bufferLabel, (nint)buffer, bufferId)
    {
        this.device     = device;
        SizeInBytes     = (uint)(Length * Unsafe.SizeOf<T>());
        handle          = buffer;
        data            = new BufferData(bufferId, Marshal.SizeOf<T>(), Length, buffer, statingHandle);
    }
    
    // --- IWgpuBuffer
    ref BufferData IWgpuBuffer.GetBufferData() => ref data;
    
    void IWgpuBuffer.ExecuteCpuCopy(void* pMapped, List<BufferRange> requestedRanges)
    {
        ReadOnlySpan<T>     gpuSourceSpan   = new ReadOnlySpan<T>(pMapped, Length);
        Span<T>             hostTargetSpan  = hostMemory.Span;
        Span<BufferRange>   ranges          = CollectionsMarshal.AsSpan(requestedRanges);

        // iterate unoptimized requested ranges
        foreach (var range in ranges)
        {
            int start   = range.start;
            int length  = range.length;

            ReadOnlySpan<T>  sourceSlice = gpuSourceSpan.Slice (start, length);
            Span<T>          targetSlice = hostTargetSpan.Slice(start, length);

            sourceSlice.CopyTo(targetSlice);
        }
    }
}

