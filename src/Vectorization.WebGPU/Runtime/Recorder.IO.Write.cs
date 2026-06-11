// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
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
    private  readonly   List<BufferRange>   tempCompactRanges   = [];
    private             WriteEntry[]        writeEntries        = [];
    
    [StackTraceHidden]
    protected override void QueueWrite(uint bufferId, int offset, int length)
    {
        var entries = writeEntries;
        if (bufferId >= entries.Length) {
            entries = ResizeWriteBuffer(bufferId);
        }
        if (!entries[bufferId].writeRanges.Add(new BufferRange(offset, length))) {
            ThrowWriteAlreadyQueued(bufferId, offset, length);
        }
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)] [StackTraceHidden]
    private void ThrowWriteAlreadyQueued(uint bufferId, int offset, int length)
    {
        var label = device.bufferMap[(int)bufferId].GetBufferData().label;
        var msg = $"a Write() of buffer view '{label}'[{offset}..{offset + length}] is already queued. You must call Submit() before you can Write() the same view again.";
        throw new InvalidOperationException(msg);
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
    private unsafe void WriteBufferRanges(uint bufferId, HashSet<BufferRange> writeRanges)
    {
        var ranges = tempWriteRanges;
        ranges.Clear();
        ranges.AddRange(writeRanges);
        writeRanges.Clear();
        
        var wgpuBuffer              = device.bufferMap[(int)bufferId];
        ref readonly var bufferData = ref wgpuBuffer.GetBufferData();
        var hostMemory              = wgpuBuffer.GetHostMemorySpan();
        
        if (ranges.Count == 1)
        {
            fixed (byte* source = hostMemory) {
                WriteRange(ranges[0], bufferData, source);
            }
        } else {
            BufferRange.GetOptimizedRanges(ranges, tempCompactRanges);
            fixed (byte* source = hostMemory) {
                WriteRangesCoalescing(tempCompactRanges, bufferData, source);
            }
        }
    }
    
    private unsafe void WriteRange(BufferRange range, in BufferData data, byte* source)
    {
        var start   = range.start  * data.elementSize;
        var length  = range.length * data.elementSize;
        wgpuQueueWriteBuffer(device.QueuePtr, data.storageHandle, (ulong)start, source + start, (nuint)length);
        if (enableTraces) {
            AddTrace(TraceType.Write, 0, 1, data.label);
        }
    }
    
    private unsafe void WriteRangesCoalescing(List<BufferRange> ranges, in BufferData data, byte* source)
    {
        var queue       = device.QueuePtr;
        var elementSize = data.elementSize;

        var firstRange  = ranges[0];
        var lastStart   = firstRange.start  * elementSize;
        var lastLength  = firstRange.length * elementSize;
        int rangeWrites = 1;

        // --- optimization: Write-Coalescing
        for (int n = 1; n < ranges.Count; n++)
        {
            var range       = ranges[n];
            var newStart    = range.start  * elementSize;
            var newLength   = range.length * elementSize;
            //       lastStart              newStart            newStart + newLength
            // ----- * -------------------- * --- newLength --- * ------------------------
            //       * -------------- length ------------------ *
            var length = newStart + newLength - lastStart;
            if (length < 4 * 1024) {
                lastLength = length;
                continue;
            }
            wgpuQueueWriteBuffer(queue, data.storageHandle, (ulong)lastStart, source + lastStart, (nuint)lastLength);
            lastStart   = newStart;
            lastLength  = newLength;
            rangeWrites++;
        }
        wgpuQueueWriteBuffer(queue, data.storageHandle, (ulong)lastStart, source + lastStart, (nuint)lastLength);
        if (enableTraces) {
            AddTrace(TraceType.Write, 0, rangeWrites, data.label, TraceSubType.Coalescing);
        }
    }
}

internal readonly struct WriteEntry
{
    internal    readonly   HashSet<BufferRange>   writeRanges = [];

    public override string ToString() => $"ranges: {writeRanges.Count}";

    public WriteEntry() { }
}