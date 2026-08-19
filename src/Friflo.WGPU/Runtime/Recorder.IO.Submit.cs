// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Friflo.GPU;
using static Friflo.WGPU.Runtime.WebGPU_native;

// ReSharper disable MergeIntoPattern
// ReSharper disable InconsistentNaming
// ReSharper disable InlineTemporaryVariable
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.Runtime;


public sealed unsafe partial class CommandRecorder
{
    internal            CommandList         commandList;
    
    /// --- thread local fields used by <see cref="WgpuIO.Submit"/>
    internal readonly   CommandListQueue    commandListQueue    = [];
    internal            BufferEntry[]       bufferEntries       = []; // ranges & segments per GpuBuffer
    internal readonly   List<ReadTexture>   readTextures        = []; // queued read texture buffer tasks
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
        return WgpuUtils.Resize(ref bufferEntries, (int)bufferId + 1);
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
    
    protected override void Submit()
    {
        ValidateThreadSafety();

        SubmitInternal();
    }
    
    protected override void ReadBuffers()
    {
        ValidateThreadSafety();

        var readSize = SubmitInternal();
        
        wgpuIO.ReadBuffers(device, readSize);
    }
    
    private uint SubmitInternal()
    {
        FinishPass();

        commandListQueue.Enqueue(commandList);
        
        var readSize = wgpuIO.Submit(this, device, currentEncoder.handle);
        commandList = device.commandListPool.Fetch(); // commandList is Return()'ed. Fetch a new one
        return readSize;
    }
}

internal readonly struct WgpuIO
{
    // --- Read Buffer ranges
    private readonly    List<List<BufferRange>> tempCompactRangesList   = [];
    private readonly    List<ActiveBuffer>      tempActiveBuffers       = [];
    private readonly    List<WgpuCommandBuffer> tempSubmitCommands      = [];
    private readonly    List<CommandList>       tempCommandLists        = [];
    
    // --- Read textures
    private readonly    List<ReadTexture>       tempActiveReadTextures  = [];
    
    public WgpuIO() { }
    
    internal unsafe uint Submit(CommandRecorder recorder, WgpuDevice device, CommandEncoder* encoder)
    {
        var createEncoder  = encoder == null;
        if (createEncoder) {
            encoder = wgpuDeviceCreateCommandEncoder(device.DevicePtr, null);
        }
        var commandListQueue    = recorder?.commandListQueue    ?? device.commandListQueue;
        var bufferEntries       = recorder?.bufferEntries       ?? device.bufferEntries;
        var readTextures        = recorder?.readTextures        ?? device.readTextures;
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
        writePos     = CopyTextures(device, readTextures, encoder, writePos);

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
    
#region buffers copy/read
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
        ReadTextures(sourceSpan, readPos);
        
        wgpuBufferUnmap(stagingBuffer);  // unmap so WGPU driver is able to access again
        
        activeBuffers.Clear();
    }
#endregion


    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void BufferMap_callback(MapAsyncStatus status, StringView message, void* userdata1, void* userdata2) {
        if (userdata1== null) return;
        var remainingMaps = (int*)userdata1;
        Interlocked.Decrement(ref *remainingMaps);
    }
    
    
#region textures - copy/read

    // READ_TEXTURE
    private unsafe uint CopyTextures(WgpuDevice device, List<ReadTexture> readTextures, CommandEncoder* encoder, uint startWritePos)
    {
        var activeReadTextures = tempActiveReadTextures;
        activeReadTextures.Clear();
        activeReadTextures.AddRange(readTextures);
        readTextures.Clear();
        
        ReadOnlySpan<ReadTexture> activeTexturesSpan = CollectionsMarshal.AsSpan(activeReadTextures);
        uint writePos = startWritePos;

        foreach (ref readonly var activeTex in activeTexturesSpan)
        {
            var srcCopy = new TexelCopyTextureInfo {
                texture  = activeTex.handle,
                mipLevel = 0,
                origin   = new Origin3D { x = 0, y = 0, z = 0 },
                aspect   = TextureAspect.All
            };
            var dstCopy = new TexelCopyBufferInfo {
                buffer = device.stagingReadBuffer.handle,
                layout = new TexelCopyBufferLayout {
                    offset       = writePos,
                    bytesPerRow  = activeTex.PaddedBytesPerRow,
                    rowsPerImage = activeTex.height
                }
            };
            var copySize = new Extent3D {
                width              = activeTex.width,
                height             = activeTex.height,
                depthOrArrayLayers = 1
            };
            wgpuCommandEncoderCopyTextureToBuffer(encoder, &srcCopy, &dstCopy, &copySize);

            writePos += activeTex.TotalPaddedSize;
        }
        return writePos;
    }

    
    private int ReadTextures(ReadOnlySpan<byte> stagingSourceSpan, int startReadPos)
    {
        ReadOnlySpan<ReadTexture> activeTexturesSpan = CollectionsMarshal.AsSpan(tempActiveReadTextures);
        int readPos = startReadPos;

        foreach (ref readonly var activeTex in activeTexturesSpan)
        {
            uint unpaddedBytesPerRow = activeTex.UnpaddedBytesPerRow;
            uint paddedBytesPerRow   = activeTex.PaddedBytesPerRow;
            Span<byte> targetSpan    = activeTex.targetMemory.Span;

            // copy line by line and skip padding
            for (int y = 0; y < activeTex.height; y++)
            {
                int srcOffset = readPos + (y * (int)paddedBytesPerRow);
                int dstOffset = y * (int)unpaddedBytesPerRow;

                var rowSource = stagingSourceSpan.Slice(srcOffset, (int)unpaddedBytesPerRow);
                var rowTarget = targetSpan.Slice(dstOffset, (int)unpaddedBytesPerRow);

                rowSource.CopyTo(rowTarget);
            }
            readPos += (int)activeTex.TotalPaddedSize;
        }

        tempActiveReadTextures.Clear();
        return readPos;
    }
#endregion
}
