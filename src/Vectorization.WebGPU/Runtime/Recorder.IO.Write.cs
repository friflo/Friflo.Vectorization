// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;
// ReSharper disable SuggestVarOrType_BuiltInTypes

// ReSharper disable InlineTemporaryVariable
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;



public sealed partial class CommandRecorder
{
    private readonly    StagingWriteBuffer  stagingWriteBuffer;
    private readonly    List<BufferIdRange> writeIdRanges       = [];
    private             WriteEntry[]        writeEntries         = [];
    
    
    protected override void QueueWrite<T>(in InOutView<T> view) {
        writeIdRanges.Add(new BufferIdRange(view.Buffer.DeviceBufferId, view.Offset, view.Length));
    }

    protected override void QueueWrite<T>(in InView<T> view) {
        writeIdRanges.Add(new BufferIdRange(view.Buffer.DeviceBufferId, view.Offset, view.Length));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private unsafe void WriteBufferRanges()
    {
        var idRanges = writeIdRanges;
        if (idRanges.Count == 0) {
            return;
        }
        var entries = writeEntries;
        
        foreach (var idRange in idRanges) {
            ref var entry = ref entries[idRange.bufferId];
            var ranges = entry.requestedRanges;
            if (ranges == null) {
                entry   = new WriteEntry(idRange.bufferId);
                ranges  = entry.requestedRanges;
            }
            ranges.Add(new BufferRange(idRange.start, idRange.length));
        }
        
        wgpuIO.WriteBuffers(device, stagingWriteBuffer, writeEntries, currentEncoder.handle);
    }
}

internal readonly partial struct WgpuIO {
    
    internal unsafe void WriteBuffers(WgpuDevice device, StagingWriteBuffer stagingWrite, WriteEntry[] writeEntries, CommandEncoder* encoder)
    {
        var activeBuffers       = tempActiveBuffers;
        var compactRangesList   = tempCompactRangesList;

        activeBuffers.Clear();
        ReadOnlySpan<IWgpuBuffer> bufferMap = CollectionsMarshal.AsSpan(device.bufferMap);

        // ---------------- copy GPU Storage [Storage] -> persistant Readback [MapRead] ----------------
        var compactRangesIndex = 0;
        uint writeSize = 0;

        foreach (var entry in writeEntries)                              // TODO iterate only until device.bufferMap.Count
        {
            var ranges = entry.requestedRanges;
            if (ranges == null || ranges.Count == 0) {
                continue;
            }
            List<BufferRange> compactRanges;
            if (compactRangesIndex++ < compactRangesList.Count) {
                compactRanges = compactRangesList[compactRangesIndex - 1];
            } else {
                compactRangesList.Add(compactRanges = new List<BufferRange>());
            }
            BufferRange.GetOptimizedRanges(ranges, compactRanges);
            ranges.Clear();
            
            ref readonly var bufferData = ref bufferMap[(int)entry.bufferId].GetBufferData();
            activeBuffers.Add(new ActiveBuffer(bufferData, compactRanges));

            foreach (var range in compactRanges) {
                writeSize += (uint)(range.length * bufferData.elementSize);
            }
        }
        
        ReadOnlySpan<ActiveBuffer> activeBuffersSpan = CollectionsMarshal.AsSpan(activeBuffers);
        
        var pMapped     = (byte*)wgpuBufferGetMappedRange(stagingWrite.handle, 0, writeSize);
        uint writePos   = 0;
        
        foreach (ref readonly var activeBuffer in activeBuffersSpan)
        {
            ref readonly var bufferData = ref activeBuffer.data;
            uint elementSize            = (uint)bufferData.elementSize;
            var wgpuBuffer              = bufferMap[bufferData.bufferId];
            
            wgpuBuffer.CopyRangesToStagingBuffer(pMapped + writePos, activeBuffer.compactRanges);
            
            foreach (var range in activeBuffer.compactRanges)
            {
                uint byteOffset = (uint)range.start  * elementSize;
                uint byteSize   = (uint)range.length * elementSize;
                
                wgpuCommandEncoderCopyBufferToBuffer(
                    encoder,
                    device.stagingReadBuffer.handle, writePos,      // source: staging ring buffer
                    bufferData.storageHandle,        byteOffset,    // target: GPU Storage [Storage]
                    byteSize
                );
                writePos += byteSize;
            }
        }
        wgpuBufferUnmap(stagingWrite.handle);
    }
}

internal readonly struct WriteEntry
{
    internal readonly   uint                bufferId;
    internal readonly   List<BufferRange>   requestedRanges;

    public override string ToString() => requestedRanges == null ? null : $"bufferId: {bufferId}  ranges: {requestedRanges.Count}";

    internal WriteEntry(uint bufferId) {
        this.bufferId       = bufferId;
        requestedRanges     = new List<BufferRange>();
    }
}