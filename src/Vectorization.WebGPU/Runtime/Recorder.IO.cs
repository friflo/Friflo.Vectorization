// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Friflo.Vectorization.GPU;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;


public sealed unsafe partial class CommandRecorder
{
    private             BufferEntry[]       bufferEntries   = [];
    private readonly    List<BufferRange>   requestedRanges = [];
    private readonly    List<BufferRange>   tempRanges      = [];
    private readonly    List<BufferData>    activeBuffers   = [];
    
    internal            PipelineRecord[]    records;
    internal            int                 recordCount;
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private SegmentMap GetBufferSegments(uint bufferId)
    {
        var entries = bufferEntries;
        if (bufferId >= entries.Length) {
            entries = ResizeBufferEntries(bufferId);
        }
        ref var entry   = ref entries[bufferId];
        var segments    = entry.bufferSegments;
        if (segments == null) {
            entry       = new BufferEntry(bufferId);
            segments    = entry.bufferSegments;
        }
        clearSegmentMaps.Add(segments);
        return segments;
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private BufferEntry[] ResizeBufferEntries(uint bufferId)
    {
        var newEntries  = new BufferEntry[bufferId + 1];
        var entries     = bufferEntries;
        Array.Copy(entries, newEntries, entries.Length);
        return bufferEntries = newEntries;
    }
    
    internal void Download()
    {
        device.Flush();
        
        if (enableDiagnostics) {
            AddRecord(PipelineRecordType.BatchSubmit);
        }        
        foreach (var range in requestedRanges) {
            bufferEntries[range.bufferId].requestedRanges.Add(range);
        }
        
        var encoder = wgpuDeviceCreateCommandEncoder(device.DevicePtr, null);
        activeBuffers.Clear();
        ReadOnlySpan<IWgpuBuffer> bufferMap = CollectionsMarshal.AsSpan(device.bufferMap);

        foreach (var bufferEntry in bufferEntries)
        {
            var ranges = bufferEntry.requestedRanges;
            if (ranges == null || ranges.Count == 0) {
                continue;
            }
            // Important: buffer must be a copy. requestedRanges is assigned with bufferEntries[].requestedRanges.
            //            They are owned by the recorder and must only be accessed in the recorder thread.
            var buffer              = bufferMap[(int)bufferEntry.bufferId].GetBufferData();
            buffer.requestedRanges  = ranges;
            activeBuffers.Add(buffer);

            var  optimizedRanges = BufferRange.GetOptimizedRanges(ranges, tempRanges);
            uint elementSize     = (uint)buffer.elementSize;
            foreach (var range in optimizedRanges)
            {
                uint byteOffset = (uint)range.start  * elementSize;
                uint byteSize   = (uint)range.length * elementSize;

                // GPU internal copy from fast compute memory in persistent stating buffer
                wgpuCommandEncoderCopyBufferToBuffer(
                    encoder,
                    buffer.storageHandle,   // source: GPU Storage [Storage]
                    byteOffset,
                    buffer.stagingHandle,   // target: persistant Readback [MapRead]
                    byteOffset,
                    byteSize
                );
            }
        }

        // finish commands and send to GPU queue
        var sendCommandBuffer = wgpuCommandEncoderFinish(encoder, null);
        wgpuQueueSubmit(device.QueuePtr, 1, &sendCommandBuffer);
        
        wgpuCommandBufferRelease(sendCommandBuffer);
        wgpuCommandEncoderRelease(encoder);

        int remainingMaps = activeBuffers.Count; // decremented to 0 if all wgpuBufferMapAsync are finished
        Span<BufferData> activeBuffersSpan = CollectionsMarshal.AsSpan(activeBuffers);
        
        foreach (ref var buffer in activeBuffersSpan)
        {
            uint totalBufferSizeInBytes = (uint)(buffer.length * buffer.elementSize);
            
            // simply map the whole memory instead of the smaller ranges
            var callbackInfo = new BufferMapCallbackInfo {
                mode        = CallbackMode.AllowProcessEvents,
                callback    = &BufferMap_callback,
                userdata1   = &remainingMaps
            };
            wgpuBufferMapAsync(buffer.stagingHandle, (ulong)MapMode.Read, 0, totalBufferSizeInBytes, callbackInfo);
        }
        // the only single CPU-Stall: wait until all buffers are mapped
        while (Thread.VolatileRead(ref remainingMaps) > 0) {
            // wgpuDeviceTick(NativePtr);
            wgpuInstanceProcessEvents(device.instance);
        }
        // direct CPU -> CPU transfer staging memory -> host memory
        foreach (ref var buffer in activeBuffersSpan)
        {
            uint totalBufferSizeInBytes = (uint)(buffer.length * buffer.elementSize);
            void* pMapped = wgpuBufferGetMappedRange(buffer.stagingHandle, 0, totalBufferSizeInBytes);
            
            var wgpuBuffer = bufferMap[buffer.bufferId];
            wgpuBuffer.ExecuteCpuCopy(pMapped, buffer.requestedRanges);     // copy staging memory to host memory
            
            wgpuBufferUnmap(buffer.stagingHandle);                          // unmap so CPU is able to access
            buffer.requestedRanges.Clear();
        }
        activeBuffers.Clear();
    }
    
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void BufferMap_callback(MapAsyncStatus status, StringView message, void* userdata1, void* userdata2) {
        if (userdata1== null) return;
        var remainingMaps = (int*)userdata1;
        Interlocked.Decrement(ref *remainingMaps);
    }
    
    // --- PipelineRecord trace
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddRecord(PipelineRecordType recordType, int kernel = 0, int calls = 0, int passes = 0)
    {
        var localRecords = records;
        if (recordCount >= localRecords.Length) {
            localRecords = ResizeRecords();
        }
        ref var record = ref localRecords[recordCount++];
        record.RecordType   = recordType;
        record.KernelId     = kernel;
        record.Calls        = calls;
        record.Passes       = passes;
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private PipelineRecord[] ResizeRecords()
    {
        var localRecords = records;
        var newRecords = new PipelineRecord[localRecords.Length * 2];
        Array.Copy(localRecords, 0, newRecords, 0, localRecords.Length);
        return records = newRecords;
    }
}
