// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;
using Buffer = Friflo.Vectorization.WebGPU.Runtime.Buffer;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable PropertyCanBeMadeInitOnly.Local
// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
// ReSharper disable SwapViaDeconstruction
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU;

[DebuggerTypeProxy(typeof(WgpuDeviceDebugView))]
public sealed unsafe partial class WgpuDevice : GpuDevice
{
    private             bool                isDisposed;
    public   override   ComputeMode         DefaultComputeMode  => ComputeMode.GPU;
    public   override   bool                IsDisposed          => isDisposed;
    
    internal readonly   Instance*           instance;
    internal            Device*             DevicePtr   { get; } 
    internal            Queue*              QueuePtr    { get; }
    internal readonly   WgpuErrorHandler    errorHandler;
    private             GCHandle            errorHandle;
    
    private  readonly   WgpuQueue           queue;
    
    private             PipelineCaches[]    pipelineCacheSlots  = [];
    private             ComputeCache[]      computeCacheSlots   = [];
    
    private             GCHandle            deviceHandle;
    private  readonly   void*               deviceHandlePtr;
    
    private  readonly   BindGroupLayoutMap  layoutCache     = new ();
    internal readonly   List<IWgpuBuffer>   bufferMap       = [];
    internal readonly   CommandListPool     commandListPool = new ();
    internal readonly   StagingReadBuffer   stagingReadBuffer;

    /// --- thread local fields used by <see cref="WgpuIO.Submit"/>
    internal readonly   CommandListQueue    commandListQueue    = [];
    internal            BufferEntry[]       bufferEntries       = [];   // ranges & segments per GpuBuffer
    private  readonly   WgpuIO              wgpuIO              = new ();
    
    
    private sealed class WgpuDeviceDebugView(WgpuDevice device)
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly WgpuDevice _device = device;

        public  string          Label       => _device.Label;
        public  bool            IsDisposed  => _device.isDisposed;
        public  PipelineContext Context     => _device.Context;
        public  GpuQueue        Queue       => _device.Queue;

        // [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
        // public WgpuDevice   RawView      => _device;
    }


    // Every class implementing IDispose must follow the same pattern. Set GpuInstance code sample.
    public override void Dispose() {
        Dispose(true);
        base.Dispose(); // calls GC.SuppressFinalize(this); to prevent execution of finalizer WHEN Dispose() is called manually
    }
    
    // A finalizer can be call from any thread.
    ~WgpuDevice() {
        Dispose(false); // false: release only native pointers
    }

    private void Dispose(bool disposing)
    {
        if (isDisposed) {
            return; // guarantees this block is executed only once
        }
        
        // Other managed objects MUST not be touched if disposing == false.
        if (disposing)
        {
            // case: only manual Dispose() call
            // TODO dispose recorder, pendingTasks & GpuEffect
            
            base.Dispose();
            
            if (DevicePtr != null) {
                wgpuDevicePoll(DevicePtr, WgpuUtils.FromBool(true), null);  // "Drain callbacks" ensure no WorkDoneCallback's are called by polling all pending callbacks
                // wgpu.DeviceSetUncapturedErrorCallback(DevicePtr, callback: default, null); // release callback before device - not relevant in v29 anymore
            }
        }
        // Native resources cleanup - cases: manual Dispose() call & finalizer calls
        // Release native resources. Order matters: first queue than device
        // Native pointer MUST be checked for null. Their creation may have failed
        foreach (var pipelineSlot in pipelineCacheSlots)
        {
            foreach (ref readonly var cache in pipelineSlot.caches.AsSpan())
            {
                if (!cache.IsCreated) continue;
                cache.bindGroupCache.Clear();
                wgpuRenderPipelineRelease(cache.renderPipeline.handle);
            }
        }
        foreach (var computeCache in computeCacheSlots)
        {
            if (!computeCache.IsCreated) continue;
            computeCache.bindGroupCache.Clear();
            wgpuComputePipelineRelease(computeCache.computePipeline.handle);
        }
        foreach (var layout in layoutCache.Values) {
            wgpuBindGroupLayoutRelease(layout.handle);
        }
        wgpuBufferRelease(stagingReadBuffer.handle);
        if (DevicePtr != null) {
            wgpuQueueRelease(QueuePtr);
            wgpuDeviceRelease(DevicePtr);
        }
        // Free anchor to managed world MUST be the last call 
        if (deviceHandle.IsAllocated) {
            deviceHandle.Free();
        }
        if (errorHandle.IsAllocated) {
            errorHandle.Free();
        }
        isDisposed = true;
    }
 
    internal WgpuDevice(
        string              label,
        WgpuErrorHandler    errorHandler,
        GCHandle            errorHandle,
        Instance*           instance,
        Device*             devicePtr,
        Queue*              queuePtr,
        int                 uniformBufferSize)
    : base(label, uniformBufferSize)
    {
        this.errorHandler   = errorHandler;
        this.errorHandle    = errorHandle;
        this.instance       = instance;
        DevicePtr           = devicePtr;
        QueuePtr            = queuePtr;
        queue               = new WgpuQueue(queuePtr);
        deviceHandle        = GCHandle.Alloc(this);
        deviceHandlePtr     = (void*)GCHandle.ToIntPtr(deviceHandle);
        
        stagingReadBuffer   = CreateStagingBuffer(16 * 1024 * 1024, "staging_read_buffer");
    }
    
    // <summary> <see cref="wgpuDevicePoll"/> should not be used anymore. Use <see cref="wgpuInstanceProcessEvents"/> instead. </summary>
    // public void Poll(bool wait) {
    //     wgpuDevicePoll(DevicePtr, WgpuUtils.FromBool(true), null);
    // }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal WgpuEncoder CreateEncoder(in StringView label)
    {
        var desc = new CommandEncoderDescriptor { label = label };
        var encoder = wgpuDeviceCreateCommandEncoder(DevicePtr, &desc);
        return new WgpuEncoder(encoder);
    }
    
    // -------------------------------- Task Dependency Tracking --------------------------------
    private static void HandleTasksFinished(QueueWorkDoneStatus status, void* userData)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)userData);
        if (handle.Target is WgpuDevice device) {
            device.inFlightCommandBufferCount--;
        }
    }

    private int inFlightCommandBufferCount;
    
    
    internal void SubmitCommands(List<WgpuCommandBuffer> commands)
    {
        int count = commands.Count;
        if (count == 0) {
            return;
        }
        inFlightCommandBufferCount = 1;
        /* // Is previous batch already send?        
        
        while (Thread.VolatileRead(ref inFlightCommandBufferCount) > 0) {
            wgpuDevicePoll(DevicePtr, WgpuUtils.FromBool(true), null); // forces "work done" callback
        } */
        Span<WgpuCommandBuffer> commandSpan = CollectionsMarshal.AsSpan(commands);
        fixed (WgpuCommandBuffer* buffer = commandSpan)
        {
            // Submit command buffers to queue
            wgpuQueueSubmit(queue.handle, (uint)count, (CommandBuffer**)buffer);
        }
        
        for (int n = 0; n < count; n++) {
            // Note: In case wgpuCommandEncoderFinish() detected a validation error
            //       releasing the handle will not decrement GpuHandleDiff.CommandBuffers
            wgpuCommandBufferRelease(commandSpan[n].handle);
        }
        
        // Register callback for the new In-Flight batch
        var callbackInfo = new QueueWorkDoneCallbackInfo {
            mode        = CallbackMode.AllowProcessEvents,
            callback    = &QueueOnSubmittedWorkDone_callback,
            userdata1   = deviceHandlePtr
        };
        var future = wgpuQueueOnSubmittedWorkDone(queue.handle, callbackInfo);

        // wait until the current batch finishes
        if  (future.id != 0 && inFlightCommandBufferCount > 0) {
            var waitInfo = new FutureWaitInfo { future = future, completed = 0 };
            wgpuInstanceWaitAny(instance, 1, &waitInfo, uint.MaxValue);
            wgpuInstanceProcessEvents(instance);
            // wgpuDevicePoll(DevicePtr, WgpuUtils.FromBool(true), null);
        }
    }
    
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void QueueOnSubmittedWorkDone_callback(QueueWorkDoneStatus status, StringView message, void* userdata1, void* userdata2) {
        HandleTasksFinished(status, userdata1);
    }
    
    // TODO - remove - kept temporary for reference
    private void Wait()
    {
        // We register a callback for completion
        bool completed = false;
        queue.OnSubmittedWorkDone(0, (QueueWorkDoneStatus status) => {
            completed = true;
        });
        while (!completed) {
            wgpuInstanceProcessEvents(instance);  // not relevant
        }
    }
        
    private Buffer* CreateBufferWithData<T>(Memory<T> data, BufferUsage usage, string bufferLabel) where T : unmanaged
    {
        fixed (void* pData = data.Span) {
            return CreateBufferWithData(pData, data.Length * sizeof(T), usage, bufferLabel);
        }
    }
    
    private Buffer* CreateBufferWithData(void* pData, int sizeInBytes, BufferUsage usage, string bufferLabel)
    {
        uint    size            = (uint)sizeInBytes;
        int     labelMaxCount   = WgpuUtils.GetMaxCount(bufferLabel);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        var len = WgpuUtils.CopySpanToBuffer(bufferLabel, labelBuffer, labelMaxCount);
        
        var desc = new BufferDescriptor {
            label           = WgpuUtils.FromPtrLength(labelBuffer, len),
            size            = size,
            usage           = (ulong)(usage | BufferUsage.CopyDst), // CopyDst to write data into
            mappedAtCreation = WgpuUtils.FromBool(true)             // We want to write now
        };
        var buffer = wgpuDeviceCreateBuffer(DevicePtr, &desc);
        
        void* pMapped = wgpuBufferGetMappedRange(buffer, 0, size);

        System.Buffer.MemoryCopy(pData, pMapped, size, size);

        wgpuBufferUnmap(buffer); // initiate copy data to GPU buffer. Returns immediately. Upload executes async.
        
        return buffer;
    }
    
    internal StagingReadBuffer CreateStagingBuffer(uint size, ReadOnlySpan<char> bufferLabel)
    {
        int     labelMaxCount   = WgpuUtils.GetMaxCount(bufferLabel);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        var len = WgpuUtils.CopySpanToBuffer(bufferLabel, labelBuffer, labelMaxCount);
        
        var desc = new BufferDescriptor {
            label           = WgpuUtils.FromPtrLength(labelBuffer, len),
            size            = size,
            usage           = (ulong)(BufferUsage.CopyDst | BufferUsage.MapRead), // read GPU buffer into staging buffer
            mappedAtCreation = WgpuUtils.FromBool(false)
        };
        var buffer = wgpuDeviceCreateBuffer(DevicePtr, &desc);
        if (buffer == null) {
            throw new Exception("GPU memory allocation failed! Insufficient VRAM or incorrect alignment");
        }
        return new StagingReadBuffer(buffer, (int)size);
    }
    
    private static BufferUsage GetBufferUsage(BufferProfile profile, BufferType type)
    {
        var usage = profile switch {
            BufferProfile.InOut     => BufferUsage.CopyDst | BufferUsage.CopySrc,
            BufferProfile.StaticIn  => BufferUsage.CopyDst,
            BufferProfile.PureOut   =>                       BufferUsage.CopySrc,
            _                       => throw new InvalidOperationException()
        };
        var typeUsage = type switch {
            BufferType.Storage      => BufferUsage.Storage,
            BufferType.Uniform      => BufferUsage.Uniform,
            BufferType.Vertex       => BufferUsage.Vertex,
            BufferType.Index        => BufferUsage.Index,
            BufferType.Indirect     => BufferUsage.Indirect,
            _                       => throw new InvalidOperationException()
        };
        return usage | typeUsage;
    }

    
    // --- GpuDevice
    public override GpuLimits GetDeviceLimits()
    {
        var limits = new Limits();
        wgpuDeviceGetLimits(DevicePtr, &limits);
        return new GpuLimits {
            MaxStorageBufferBindingSize         = (long)limits.maxStorageBufferBindingSize,  
            MaxComputeWorkgroupStorageSize      = (int) limits.maxComputeWorkgroupStorageSize, 
            MaxBindGroups                       = (int) limits.maxBindGroups, 
            MaxComputeInvocationsPerWorkgroup   = (int) limits.maxComputeInvocationsPerWorkgroup, 
        };
    }
    
    public override GpuBuffer<T> CreateBuffer<T>(Memory<T> data, string bufferLabel, BufferProfile profile, BufferType type = BufferType.Storage)
    {
        var wgpuUsage       = GetBufferUsage(profile, type);
        var handle          = CreateBufferWithData(data, wgpuUsage, bufferLabel);
        var gpuBuffer       = new WgpuBuffer<T>(this, handle, bufferMap.Count, data, bufferLabel);
        bufferMap.Add(gpuBuffer);
        return gpuBuffer;
    }

    protected override PipelineContext NewPipelineContext() => new CommandRecorder(this);
    
    
    // --- CommandStream
    [StackTraceHidden]
    internal void ValidateThreadSafety()
    {
        if (threadId == Environment.CurrentManagedThreadId) {
            return;
        }
        throw new InvalidOperationException(
            $"[Thread Context Violation] method executes on thread: {Environment.CurrentManagedThreadId} but GpuDevice belongs to thread {threadId}!");
    }
    
    protected override void ReadBuffers()
    {
        ValidateThreadSafety();
        
        if (commandListQueue.IsEmpty) {
            return;
        }
        var count   = bufferMap.Count;
        var entries = bufferEntries;
        
        if (entries.Length < count) {
            var newEntries = new  BufferEntry[Math.Max(2 * entries.Length, count)];
            Array.Copy(entries, 0, newEntries, 0, entries.Length);
            bufferEntries = newEntries;
        }
        var readSize = wgpuIO.Submit(null, this, null);
        wgpuIO.ReadBuffers(this, readSize);
    }
}

