// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InlineTemporaryVariable
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;

public sealed partial class CommandRecorder
{
    // --- Write Buffer ranges 
    private  readonly   List<BufferRange>   tempWriteRanges     = [];
    private  readonly   StagingWriteBuffer  stagingWriteBuffer  = new ();
    private             WriteEntry[]        writeEntries        = [];
    
    protected override void QueueWrite(uint bufferId, int offset, int length)
    {
        var entries = writeEntries;
        if (bufferId >= entries.Length) {
            entries = ResizeWriteBuffer(bufferId);
        }
        entries[bufferId].writeRanges.Add(new BufferRange(offset, length));
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private WriteEntry[] ResizeWriteBuffer(uint bufferId)
    {
        var entries = writeEntries;
        var newEntries = new WriteEntry[Math.Max(2 * entries.Length, bufferId + 1)];
        Array.Copy(entries, 0, newEntries, 0, entries.Length);
        
        for (int n = entries.Length; n < newEntries.Length; n++) {
            newEntries[n] = new WriteEntry();
        }
        return writeEntries = newEntries;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteBufferRanges(uint bufferId)
    {
        var entries = writeEntries;
        if (bufferId >= entries.Length) {
            return;
        }
        var writeRanges = entries[bufferId].writeRanges;
        if (writeRanges.Count == 0) {
            return;
        }
        WriteBufferRanges(bufferId, writeRanges);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private unsafe void WriteBufferRanges(uint bufferId, List<BufferRange> writeRanges)
    {
        BufferRange.GetOptimizedRanges(writeRanges, tempWriteRanges);
        
        writeRanges.Clear();
        
        var wgpuBuffer              = device.bufferMap[(int)bufferId];
        ref readonly var bufferData = ref wgpuBuffer.GetBufferData();

        var byteLength = wgpuBuffer.CopyRangesToStagingBuffer(stagingWriteBuffer, tempWriteRanges);
        
        fixed (void* source = stagingWriteBuffer.targetBuffer) {
            wgpuQueueWriteBuffer(device.QueuePtr, bufferData.storageHandle, 0, source, (nuint)byteLength);
        }
    }
}

internal class StagingWriteBuffer
{
    internal    byte[]  targetBuffer  = new byte [1 * 1024 * 1024];
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal byte[] ResizeStagingWriteBuffer(int size)
    {
        var buffer      = targetBuffer;
        var newBuffer   = new byte[Math.Max(2 * buffer.Length, size)];
        Array.Copy(buffer, 0, newBuffer, 0, buffer.Length);
        return targetBuffer = newBuffer;
    }
}

internal readonly struct WriteEntry
{
    internal    readonly   List<BufferRange>   writeRanges = [];

    public override string ToString() => $"ranges: {writeRanges.Count}";

    public WriteEntry() { }
}