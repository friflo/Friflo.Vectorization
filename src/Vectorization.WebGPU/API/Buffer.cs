// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
    internal    ref readonly BufferData GetBufferData();
    internal    int          ExecuteCpuCopy(byte* pMapped, List<BufferRange> compactRanges);
    internal    int          CopyRangesToStagingBuffer(StagingWriteBuffer stagingWriteBuffer, List<BufferRange> compactRanges);
}

public sealed unsafe class WgpuBuffer<T> : GpuBuffer<T>, IWgpuBuffer where T : unmanaged
{
    internal            Buffer*     handle { get; private set; }
    private             WgpuDevice  device { get; set; }
    private  readonly   BufferData  data;
    
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
        handle = null;
        device = null;
    }

    internal WgpuBuffer(WgpuDevice device, Buffer* buffer, int bufferId, Memory<T> hostMemory, string bufferLabel)
        : base(hostMemory, bufferLabel, (nint)buffer, bufferId)
    {
        this.device     = device;
        handle          = buffer;
        data            = new BufferData(bufferId, Marshal.SizeOf<T>(), buffer);
    }
    
    // --- IWgpuBuffer
    ref readonly BufferData IWgpuBuffer.GetBufferData() => ref data;
    
    int IWgpuBuffer.ExecuteCpuCopy(byte* pMapped, List<BufferRange> compactRanges)
    {
        Span<T>                     hostTargetSpan  = hostMemory.Span;
        ReadOnlySpan<BufferRange>   ranges          = CollectionsMarshal.AsSpan(compactRanges);
        var readPos = 0;

        foreach (var range in ranges)
        {
            int start   = range.start;
            int length  = range.length;
            
            ReadOnlySpan<T>  gpuSourceSpan  = new ReadOnlySpan<T>(pMapped + readPos, length);
            Span<T>          targetSlice    = hostTargetSpan.Slice(start,            length);

            gpuSourceSpan.CopyTo(targetSlice);
            
            readPos       += length * sizeof(T);
        }
        return readPos;
    }
    
    int IWgpuBuffer.CopyRangesToStagingBuffer(StagingWriteBuffer stagingWriteBuffer, List<BufferRange> compactRanges)
    {
        ReadOnlySpan<T> hostSourceSpan  = hostMemory.Span;
        Span<byte>      targetSpan      = stagingWriteBuffer.targetBuffer.AsSpan();
        int             writePos        = 0;
        
        foreach (var range in compactRanges)
        {
            var byteSize    = range.length * data.elementSize;
            var nextPos     = writePos + byteSize;
            if (nextPos > targetSpan.Length) {
                targetSpan = stagingWriteBuffer.ResizeStagingWriteBuffer(nextPos).AsSpan();
            }
            ReadOnlySpan<T> rangeSource = hostSourceSpan.Slice(range.start, range.length);
            Span<T> rangeTarget         = MemoryMarshal.Cast<byte, T>(targetSpan.Slice(writePos, byteSize));
            rangeSource.CopyTo(rangeTarget);
            
            writePos = nextPos;
        }
        return writePos;
    }
}

