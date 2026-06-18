// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Friflo.Vectorization.GPU;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable MergeIntoPattern
// ReSharper disable InconsistentNaming
// ReSharper disable InlineTemporaryVariable
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;


public sealed unsafe partial class CommandRecorder
{
    internal            CommandList         commandList;
    
    /// --- thread local fields used by <see cref="WgpuIO.Submit"/>
    internal readonly   CommandListQueue    commandListQueue    = [];
    internal            BufferEntry[]       bufferEntries       = []; // ranges & segments per GpuBuffer
    private  readonly   WgpuIO              wgpuIO              = new ();
    
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
    
    protected override void QueueRead(uint bufferId, int offset, int length) {
        commandList.idRanges.Add(new BufferIdRange(bufferId, offset, length));
    }

    public override  void FlushTo(PipelineContext targetContext) {
        ValidateThreadSafety();
        FlushTo(((CommandRecorder)targetContext).commandListQueue);
    }
    
    public override  void FlushTo(GpuDevice targetDevice) {
        ValidateThreadSafety();
        FlushTo(((WgpuDevice)targetDevice).commandListQueue);
    }
    
    private void FlushTo(CommandListQueue targetQueue)
    {
        FinishPass();
        FinishEncoder("FlushTo"u8); // creates CommandBuffer and adds it to commandList.commands

        var queue               = commandListQueue;
        var localCommandList    = commandList;
        if (localCommandList.commands.Count > 0) {        
            queue.Enqueue(localCommandList); // add commands currently in flight
            commandList = device.commandListPool.Fetch();
        }
        if (queue.IsEmpty) {
            return;
        }

        // queue is empty after iteration
        while (queue.TryDequeue(out var list)) {
            targetQueue.Enqueue(list);
        }
    }
    
    protected override void ReadBuffers()
    {
        ValidateThreadSafety();

        FinishPass();

        commandListQueue.Enqueue(commandList);
        
        var readSize = wgpuIO.Submit(this, device, currentEncoder.handle);
        commandList = device.commandListPool.Fetch(); // commandList is Return()'ed. Fetch a new one
        
        wgpuIO.ReadBuffers(device, readSize);
    }
}

internal readonly struct WgpuIO
{
    // --- Read Buffer ranges
    private readonly    List<List<BufferRange>> tempCompactRangesList   = [];
    private readonly    List<ActiveBuffer>      tempActiveBuffers       = [];
    private readonly    List<WgpuCommandBuffer> tempSubmitCommands      = [];
    private readonly    List<CommandList>       tempCommandLists        = [];
    
    public WgpuIO() { }
    
    internal unsafe uint Submit(CommandRecorder recorder, WgpuDevice device, CommandEncoder* encoder)
    {
        var createEncoder  = encoder == null;
        if (createEncoder) {
            encoder = wgpuDeviceCreateCommandEncoder(device.DevicePtr, null);
        }
        var commandListQueue    = recorder?.commandListQueue    ?? device.commandListQueue;
        var bufferEntries       = recorder?.bufferEntries       ?? device.bufferEntries;
        var submitCommands      = tempSubmitCommands;
        var commandLists        = tempCommandLists;
        
        // process commandList.ranges before submitting commandList
        submitCommands.Clear();
        commandLists.Clear();
        
        // iterate and clear commandListQueue
        while(commandListQueue.TryDequeue(out var commandList))
        {
            foreach (var range in commandList.idRanges) {
                ref var entry = ref bufferEntries[range.bufferId];
                var ranges = entry.requestedRanges;
                if (ranges == null) {
                    entry   = new BufferEntry(range.bufferId);
                    ranges  = entry.requestedRanges;
                }
                ranges.Add(new BufferRange(range.start, range.length));
            }
            submitCommands.AddRange(commandList.commands);
            commandLists.Add(commandList);
        }
        
        var writePos = CopyBuffers(device, bufferEntries, encoder);

        // --------------------- append copyBufferCommands and submit ---------------------
        if (createEncoder) {
            var copyBufferCommands = wgpuCommandEncoderFinish(encoder, null);
            wgpuCommandEncoderRelease(encoder);
            submitCommands.Add(new WgpuCommandBuffer(copyBufferCommands));
        }
        if (recorder != null) {
            if (!createEncoder) {
                recorder.FinishEncoder("BatchedCommands"u8);    // creates CommandBuffer and adds it to 
                var commands = recorder.commandList.commands;   // recorder.commandList.commands
                submitCommands.Add(commands[^1]);
            }
            if (recorder.enableTraces) {
                recorder.AddTrace(TraceType.Submit, 0, submitCommands.Count);
            }
        }
        
        // At this point no read / write access to CommandList's -> safe to return to pool 
        foreach (var commandList in commandLists) {
            device.commandListPool.Return(commandList); // clears: ranges & commands
        }
        commandLists.Clear();
        
        device.SubmitCommands(submitCommands);
        
        return writePos;
    }
    
    private unsafe uint CopyBuffers(WgpuDevice device, BufferEntry[] bufferEntries, CommandEncoder* encoder)
    {
        var activeBuffers       = tempActiveBuffers;
        var compactRangesList   = tempCompactRangesList;

        activeBuffers.Clear();
        ReadOnlySpan<IWgpuBuffer> bufferMap = CollectionsMarshal.AsSpan(device.bufferMap);

        // ---------------- copy GPU Storage [Storage] -> persistant Readback [MapRead] ----------------
        var compactRangesIndex = 0;

        foreach (var bufferEntry in bufferEntries)                              // TODO iterate only until device.bufferMap.Count
        {
            var ranges = bufferEntry.requestedRanges;
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
            
            ref readonly var bufferData = ref bufferMap[(int)bufferEntry.bufferId].GetBufferData();
            activeBuffers.Add(new ActiveBuffer(bufferData, compactRanges));
        }
        
        uint writePos = 0;
        // Encode GPU copy commands. New loop ensures all requestedRanges are cleared in previous loop.
        ReadOnlySpan<ActiveBuffer> activeBuffersSpan = CollectionsMarshal.AsSpan(activeBuffers);
        foreach (ref readonly var activeBuffer in activeBuffersSpan)
        {
            ref readonly var bufferData = ref activeBuffer.data;
            uint elementSize            = (uint)bufferData.elementSize;
            
            foreach (var range in activeBuffer.compactRanges)
            {
                uint byteOffset = (uint)range.start  * elementSize;
                uint byteSize   = (uint)range.length * elementSize;

                // GPU internal copy from fast compute memory in persistent stating buffer
                wgpuCommandEncoderCopyBufferToBuffer(
                    encoder,
                    bufferData.storageHandle,        byteOffset,    // source: GPU Storage [Storage]
                    device.stagingReadBuffer.handle, writePos,      // target: persistant Readback [MapRead]
                    byteSize
                );
                writePos += byteSize;
            }
        }
        return writePos;
    }
    
        
    internal unsafe void ReadBuffers(WgpuDevice device, uint readSize)
    {
        if(readSize == 0) {
            return;
        }
        ReadOnlySpan<IWgpuBuffer> bufferMap = CollectionsMarshal.AsSpan(device.bufferMap);
        var activeBuffers   = tempActiveBuffers;
        var stagingBuffer   = device.stagingReadBuffer.handle;
        
        // --------------------- map all GpuBuffer's that are read from GPU --------------------- 
        int remainingMaps = 1;
        ReadOnlySpan<ActiveBuffer> activeBuffersSpan = CollectionsMarshal.AsSpan(activeBuffers);
            
        // simply map the whole memory instead of the smaller ranges 
        var callbackInfo = new BufferMapCallbackInfo {
            mode        = CallbackMode.AllowProcessEvents,
            callback    = &BufferMap_callback,
            userdata1   = &remainingMaps                                // TODO FIX ME - use instance variable
        };
        wgpuBufferMapAsync(stagingBuffer, (ulong)MapMode.Read, 0, readSize, callbackInfo);

        
        // the only single CPU-Stall: wait until stagingBuffer is mapped
        while (Thread.VolatileRead(ref remainingMaps) > 0) {
            // wgpuDeviceTick(NativePtr);
            wgpuInstanceProcessEvents(device.instance);
        }
        
        // ------------------ copy WGPU driver CPU memory -> CPU host memory ------------------
        var source      = (byte*)wgpuBufferGetMappedRange(stagingBuffer, 0, readSize);
        var sourceSpan  = new ReadOnlySpan<byte>(source, (int)readSize);
        int readPos     = 0;

        foreach (ref readonly var activeBuffer in activeBuffersSpan)
        {
            ref readonly var bufferData = ref activeBuffer.data;
            var elementSize             = bufferData.elementSize;
            var wgpuBuffer              = bufferMap[bufferData.bufferId];
            var targetSpan              = wgpuBuffer.GetHostMemorySpan();
            
            foreach (var range in activeBuffer.compactRanges) {
                int size        = range.length * elementSize;
                var rangeTarget = targetSpan.Slice(range.start  * elementSize, size);
                var rangeSource = sourceSpan.Slice(readPos,                    size);
                rangeSource.CopyTo(rangeTarget);
                readPos += size;
            }
        }
        wgpuBufferUnmap(stagingBuffer);  // unmap so WGPU driver is able to access again
        
        activeBuffers.Clear();
    }
    
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void BufferMap_callback(MapAsyncStatus status, StringView message, void* userdata1, void* userdata2) {
        if (userdata1== null) return;
        var remainingMaps = (int*)userdata1;
        Interlocked.Decrement(ref *remainingMaps);
    }
}
