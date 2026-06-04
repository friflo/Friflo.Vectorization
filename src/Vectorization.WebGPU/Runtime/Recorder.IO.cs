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
    
    /// --- thread local fields used by <see cref="SubmitIO.SubmitReadBuffers"/>
    internal readonly   CommandListQueue    commandListQueue    = [];
    internal            BufferEntry[]       bufferEntries       = []; // ranges & segments per GpuBuffer
    private  readonly   SubmitIO            submitIO            = new ();
    
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
    
    public override  void FlushQueueTo(PipelineContext targetContext) {
        ValidateThreadSafety();
        FlushQueueTo(((CommandRecorder)targetContext).commandListQueue);
    }
    
    public override  void FlushQueueTo(GpuDevice targetDevice) {
        ValidateThreadSafety();
        FlushQueueTo(((WgpuDevice)targetDevice).commandListQueue);
    }
    
    private void FlushQueueTo(CommandListQueue targetQueue)
    {
        if (PassBatching == PassBatching.HazardDriven && renderPassCount > 0) {
            FinishPass();
            FinishEncoder("FlushTo"u8); // creates CommandBuffer and adds it to commandList.commands
        }
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
        
        if (PassBatching == PassBatching.HazardDriven && renderPassCount > 0) {
            FinishPass();
        }
        commandListQueue.Enqueue(commandList);
        
        submitIO.SubmitReadBuffers(this, device, currentEncoder.handle);
        
        commandList = device.commandListPool.Fetch(); // commandList is Return()'ed. Fetch a new one
    }
}

internal readonly struct SubmitIO
{
    private readonly    List<BufferIdRange>     tempIdRanges        = [];
    private readonly    List<BufferData>        tempActiveBuffers   = [];
    private readonly    List<WgpuCommandBuffer> tempSubmitCommands  = [];
    private readonly    List<CommandList>       tempCommandLists    = [];
    
    public SubmitIO() {}
    
    internal unsafe void SubmitReadBuffers(
        CommandRecorder     recorder,
        WgpuDevice          device,
        CommandEncoder*     encoder)
    {
        var createEncoder  = encoder == null;
        if (createEncoder) {
            encoder = wgpuDeviceCreateCommandEncoder(device.DevicePtr, null);
        }
        var commandListQueue    = recorder?.commandListQueue    ?? device.commandListQueue;
        var bufferEntries       = recorder?.bufferEntries       ?? device.bufferEntries;
        var activeBuffers       = tempActiveBuffers;
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
                var idRanges = entry.requestedIdRanges;
                if (idRanges == null) {
                    entry   = new BufferEntry(range.bufferId);
                    idRanges  = entry.requestedIdRanges;
                }
                idRanges.Add(range);
            }
            submitCommands.AddRange(commandList.commands);
            commandLists.Add(commandList);
        }
        
        activeBuffers.Clear();
        ReadOnlySpan<IWgpuBuffer> bufferMap = CollectionsMarshal.AsSpan(device.bufferMap);

        // ---------------- copy GPU Storage [Storage] -> persistant Readback [MapRead] ----------------
        foreach (var bufferEntry in bufferEntries)                              // TODO iterate only until device.bufferMap.Count
        {
            var idRanges = bufferEntry.requestedIdRanges;
            if (idRanges == null || idRanges.Count == 0) {
                continue;
            }
            // Important: buffer must be a copy. requestedRanges is assigned with bufferEntries[].requestedRanges.
            //            They are owned by the recorder and must only be accessed in the recorder thread.
            ref readonly var buffer = ref bufferMap[(int)bufferEntry.bufferId].GetBufferData();
            activeBuffers.Add(buffer);

            var  optimizedRanges = BufferRange.GetOptimizedRanges(idRanges, tempIdRanges);
            uint elementSize     = (uint)buffer.elementSize;
            foreach (var range in optimizedRanges)
            {
                uint byteOffset = (uint)range.start  * elementSize;
                uint byteSize   = (uint)range.length * elementSize;

                // GPU internal copy from fast compute memory in persistent stating buffer
                wgpuCommandEncoderCopyBufferToBuffer(
                    encoder,    buffer.storageHandle,   // source: GPU Storage [Storage]
                    byteOffset, buffer.stagingHandle,   // target: persistant Readback [MapRead]
                    byteOffset, byteSize
                );
            }
        }

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

        
        // --------------------- map all GpuBuffer's that are read from GPU --------------------- 
        int remainingMaps = activeBuffers.Count; // decremented to 0 if all wgpuBufferMapAsync are finished
        Span<BufferData> activeBuffersSpan = CollectionsMarshal.AsSpan(activeBuffers);
        
        foreach (ref var buffer in activeBuffersSpan)
        {
            uint totalBufferSizeInBytes = (uint)(buffer.length * buffer.elementSize);
            
            // simply map the whole memory instead of the smaller ranges    // TODO use optimized Ranges to map only required ranges 
            var callbackInfo = new BufferMapCallbackInfo {
                mode        = CallbackMode.AllowProcessEvents,
                callback    = &BufferMap_callback,
                userdata1   = &remainingMaps                                // TODO FIX ME - use instance variable
            };
            wgpuBufferMapAsync(buffer.stagingHandle, (ulong)MapMode.Read, 0, totalBufferSizeInBytes, callbackInfo);
        }
        // the only single CPU-Stall: wait until all buffers are mapped
        while (Thread.VolatileRead(ref remainingMaps) > 0) {
            // wgpuDeviceTick(NativePtr);
            wgpuInstanceProcessEvents(device.instance);
        }
        
        // --------------------- direct CPU -> CPU transfer staging memory -> host memory --------------------- 
        foreach (ref var buffer in activeBuffersSpan)
        {
            uint totalBufferSizeInBytes = (uint)(buffer.length * buffer.elementSize);
            void* pMapped = wgpuBufferGetMappedRange(buffer.stagingHandle, 0, totalBufferSizeInBytes);
            
            var wgpuBuffer  = bufferMap    [buffer.bufferId];
            var idRanges    = bufferEntries[buffer.bufferId].requestedIdRanges;
            wgpuBuffer.ExecuteCpuCopy(pMapped, idRanges);         // copy staging memory to host memory
            
            wgpuBufferUnmap(buffer.stagingHandle);              // unmap so CPU is able to access
            idRanges.Clear();
        }
        activeBuffers.Clear();
    }
    
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void BufferMap_callback(MapAsyncStatus status, StringView message, void* userdata1, void* userdata2) {
        if (userdata1== null) return;
        var remainingMaps = (int*)userdata1;
        Interlocked.Decrement(ref *remainingMaps);
    }
}
