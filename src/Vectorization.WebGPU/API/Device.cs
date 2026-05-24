// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;
using Buffer = Friflo.Vectorization.WebGPU.Runtime.Buffer;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
// ReSharper disable SwapViaDeconstruction
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU;

//      Feature Set / Properties
//      -------------------------
// Core Architecture & Philosophy
//  - Zero-Allocation Steady State:     no managed allocation during main execution loop
//  - Mechanical Sympathy Design:       Focus on CPU cache efficiency.
//  - Stateless Execution Flow:         High-level logic is decoupled from resource management
// Deferred Batch Execution
//  - Atomic Batch Dispatching:         Instead of immediate, chatty execution, all GPU operations are recorded into command buffer and submitted in a single, coherent batch
//  - Command Buffer Orchestration:     Leverages pre-allocated buffers to sequence complex compute chains, ensuring that the GPU never stalls waiting for the next instruction.
// GPU & Compute Capabilities
//  - Cross-Backend Compatibility:      unified API for Vulkan, DirectX 12, and Metal
//  - Hybrid Compute Support:           Seamlessly switch between Hardware Acceleration (GPU), AVX/SIMD or Scalar
// Resource & Thread Management     
//  - Thread-Safe Command Dispatch      Designed for multithreaded environments
//  - Low-Overhead Resource Pooling     Efficient "Rent/Return" patterns for Tasks and Buffers to maintain a fixed memory footprint
//  - Type-Safe Buffer Abstraction      GpuBuffer<T> system bridges the gap between managed C# types and raw GPU memory.
// Developer Ergonomics
//  - Lean Codebase                     less than 40 KB minimizing instruction cache misses
//  - Compile-Time Safety               Heavy use of generics and constraints to catch errors at compile time / IDE
public sealed unsafe class WgpuDevice : GpuDevice
{
    private             bool                isDisposed;
    public   override   ComputeMode         DefaultComputeMode  => ComputeMode.GPU;
    public   override   bool                IsDisposed          => isDisposed;
    internal readonly   Instance*           instance;
    internal            Device*             DevicePtr   { get; } 
    internal            Queue*              QueuePtr    { get; }
    internal readonly   WgpuErrorHandler    errorHandler;
    private             GCHandle            errorHandle;
    
    public   readonly   CommandRecorder     Recorder;
    // private          TaskArray           availableTasks;     TASK_TAG
    internal readonly   WgpuBuffer<byte>    globalUniformPool;
    private  readonly   WgpuQueue           queue;
    
    private  static     int                 effectSlotCount;
    private             WgpuEffect[]        effectSlots  	= new WgpuEffect[4];
    // private          TaskArray           pendingTasks;       TASK_TAG
    // private          TaskArray           inFlightTasks;      TASK_TAG
    private             GCHandle            deviceHandle;
    private  readonly   void*               deviceHandlePtr;
    
    private  static      int                 layoutCacheCount;
    private             CachedGroupLayout[] layoutCache  = new CachedGroupLayout[64];
    internal readonly   List<BufferEntry>   bufferEntries = new ();


    // Every class implementing IDispose must follow the same pattern. Set GpuInstance code sample.
    public override void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this); // prevent execution of finalizer WHEN Dispose() is called manually
    }
    
    // A finalizer can be call from any thread.
    ~WgpuDevice() {
        Dispose(false); // false: release only native pointers
    }

    private void Dispose(bool disposing)
    {
        if (isDisposed) return;  // guarantees this block is executed only once

        // Other managed objects MUST not be touched if disposing == false.
        if (disposing) {
            // case: only manual Dispose() call
            globalUniformPool?.Dispose();
            // TODO dispose recorder, pendingTasks & GpuEffect
            
            if (DevicePtr != null) {
                if (QueuePtr != null) {
                    Flush(wait: true); // flush all pending GPU operations
                    wgpuDevicePoll(DevicePtr, WgpuUtils.FromBool(true), null); // "Drain callbacks" ensure no WorkDoneCallback's are called by polling all pending callbacks
                }
                // wgpu.DeviceSetUncapturedErrorCallback(DevicePtr, callback: default, null); // release callback before device - not relevant in v29 anymore
            }
        }
        // Native resources cleanup - cases: manual Dispose() call & finalizer calls
        // Release native resources. Order matters: first queue than device
        // Native pointer MUST be checked for null. Their creation may have failed
        
        for (int n = 0; n < effectSlots.Length; n++) {
            ref var effect = ref effectSlots[n];
            effect.bufferCache.Release();
            if(effect.IsCreated) {
                if (effect.pipeline.handle != null) wgpuComputePipelineRelease(effect.pipeline.handle);
            }
        }
        var cache = layoutCache;
        for (int n = 0; n < cache.Length; n++) {
            if (cache[n].layout.IsCreated) wgpuBindGroupLayoutRelease(cache[n].layout.handle);
            cache[n] = default;
        }
        // Important: Queue* must not be released. It shares the same lifetime as Device*.
        //  if (QueuePtr != null) {
        //      wgpu.QueueRelease(QueuePtr); will cause segtfault/panic when calling wgpu.QueueSubmit()
        //  }
        if (DevicePtr != null) {
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
    
    
    /* [MethodImpl(MethodImplOptions.AggressiveInlining)]     // TASK_TAG
    public WgpuTask RentTask() {
        lock (availableTasks.tasks) {
            return availableTasks.Pop();
        } 
    }

    internal void ReturnTask(WgpuTask task)
    {
        task.Reset();
        lock (availableTasks.tasks) {
            availableTasks.Push(task);
        }
    } */
    
 
    // --- effectSlots
    // NewGpuEffectSlot() is called only once per shadow method. It stores the slot index in a static readonly int
    public static int NewEffectSlot() => Interlocked.Increment(ref effectSlotCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref WgpuEffect GetEffect(int slot) {
        var slots = effectSlots;
        if (slot < slots.Length) {
            return ref slots[slot];
        }
        return ref MissingEffect;
    }
    
    private static WgpuEffect MissingEffect;
    
    public ref WgpuEffect CreateEffect(
        int                 slot,
        WgpuComputePipeline  pipeline,
        WgpuBindGroupLayout bufferLayout,
        WgpuBindGroupLayout uniformLayout)
    {
        var slots = effectSlots;
        if (slot >= slots.Length) {
            var newSlots = new WgpuEffect[Math.Max(2 * slots.Length, slot + 1)];
            Array.Copy(slots, newSlots, slots.Length);
            slots = effectSlots = newSlots;
        }
        slots[slot] = new WgpuEffect(pipeline, bufferLayout, uniformLayout);
        return ref slots[slot];
    }
    
    public void UpdateBufferCache(int slot, WgpuBindGroup bindGroup, ulong hash) {
        effectSlots[slot].bufferCache.Update(bindGroup, hash);
    }

    internal WgpuDevice(
        string              label,
        WgpuErrorHandler    errorHandler,
        GCHandle            errorHandle,
        Instance*           instance,
        Device*             devicePtr,
        Queue*              queuePtr,
        int                 maxTasks,
        int                 slotSize)
    : base(label, slotSize)
    {
        this.errorHandler   = errorHandler;
        this.errorHandle    = errorHandle;
        this.instance       = instance;
        DevicePtr           = devicePtr;
        QueuePtr            = queuePtr;
        queue               = new WgpuQueue(this, queuePtr);
        deviceHandle        = GCHandle.Alloc(this);
        deviceHandlePtr     = (void*)GCHandle.ToIntPtr(deviceHandle);
        
        globalUniformPool   = (WgpuBuffer<byte>)CreateBuffer<byte>(maxTasks * slotSize, GpuBufferUsage.Uniform | GpuBufferUsage.CopyDst, "globalUniformPool");
        Recorder            = new CommandRecorder(this);
        /* taskPool            = new WgpuTask[maxTasks];    TASK_TAG
         availableTasks      = new TaskArray(maxTasks);
        pendingTasks        = new TaskArray(maxTasks);
        inFlightTasks       = new TaskArray(maxTasks);
        for (int i = 0; i < maxTasks; i++) {
            var task = new WgpuTask(this);
            taskPool[i] = task;
            availableTasks.Push(task);
        } */
    }
    
    // <summary> <see cref="wgpuDevicePoll"/> should not be used anymore. Use <see cref="wgpuInstanceProcessEvents"/> instead. </summary>
    // public void Poll(bool wait) {
    //     wgpuDevicePoll(DevicePtr, WgpuUtils.FromBool(true), null);
    // }

    internal WgpuEncoder CreateEncoder(CommandRecorder recorder, ReadOnlySpan<byte> encoderLabel)
    {
        fixed (byte* labelPtr = encoderLabel)
        {
            var desc = new CommandEncoderDescriptor {
                label = WgpuUtils.FromPtrSpan(labelPtr, encoderLabel)
            };
            var encoder = wgpuDeviceCreateCommandEncoder(DevicePtr, &desc);
            return new WgpuEncoder(recorder, encoder);
        }
    }

    internal void WriteBuffer<T>(WgpuBuffer<T> buffer, uint byteOffset, void* data, uint byteSize) where T : unmanaged {
        queue.WriteBuffer(buffer.handle, byteOffset, data, byteSize);
    }
    
    // -------------------------------- Task Dependency Tracking --------------------------------
    private static void HandleTasksFinished(QueueWorkDoneStatus status, void* userData)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)userData);
        if (handle.Target is WgpuDevice device) {
            device.inFlightCommandBufferCount--;
        }
    }
    
    /* private void ReturnPendingTasks() {      					// TASK_TAG
         // Be ultra safe. DevicePoll() in Dispose(disposing) should already ensure HandleTasksFinished() is not fired anymore
        if (isDisposed) return; 
        for (int i = 0; i < inFlightTasks.count; i++) {
            var task = inFlightTasks.tasks[i];
            ReturnTask(task);
        }
        inFlightTasks.Clear();
    } */
    
    /* [MethodImpl(MethodImplOptions.AggressiveInlining)]   		TASK_TAG
    public void Enqueue(WgpuTask task)
    {
        pendingTasks.Push(task);
        if (pendingTasks.count >= 1024) { 
            Flush(); // ensure list does not grow unlimited
        }
    } */
    
    private int inFlightCommandBufferCount;
    
    public override void Flush(bool wait = true)
    {
        // var tasks = pendingTasks;
        int count = Recorder.commandBuffers.Count;
        if (count == 0 && !wait) return;
        inFlightCommandBufferCount = 1;
        /* // Is previous batch already send?
        
        while (Thread.VolatileRead(ref inFlightCommandBufferCount) > 0) {
            wgpuDevicePoll(DevicePtr, WgpuUtils.FromBool(true), null); // forces "work done" callback
        } */
        var future = new Future();
        if (count > 0) {
            // Submit command buffers to queue
            var commandBuffers = stackalloc CommandBuffer*[count];
            for (int n = 0; n < count; n++) {
                commandBuffers[n] = (CommandBuffer*)Recorder.commandBuffers[n];
            }
            wgpuQueueSubmit(queue.handle, (uint)count, commandBuffers);
            
            Recorder.commandBuffers.Clear();
            for (int n = 0; n < count; n++) {
                // Note: In case wgpuCommandEncoderFinish() detected a validation error
                //       releasing the handle will not decrement GpuHandleDiff.CommandBuffers
                wgpuCommandBufferRelease(commandBuffers[n]);
            }
            /* // Swap list references   TASK_TAG
            var temp        = inFlightTasks;
            inFlightTasks   = tasks;
            pendingTasks    = temp; */
            
            // Register callback for the new In-Flight batch
            var callbackInfo = new QueueWorkDoneCallbackInfo {
                mode        = CallbackMode.AllowProcessEvents,
                callback    = &QueueOnSubmittedWorkDone_callback,
                userdata1   = deviceHandlePtr
            };
            future = wgpuQueueOnSubmittedWorkDone(queue.handle, callbackInfo);
        }
        // If deterministic result is required, wait until the current batch finishes
        if (wait) {
            if  (future.id != 0 && inFlightCommandBufferCount > 0) {
                var waitInfo = new FutureWaitInfo { future = future, completed = 0 };
                wgpuInstanceWaitAny(instance, 1, &waitInfo, uint.MaxValue);
                wgpuInstanceProcessEvents(instance);
                // wgpuDevicePoll(DevicePtr, WgpuUtils.FromBool(true), null);
            }
        }
    }
    
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void QueueOnSubmittedWorkDone_callback(QueueWorkDoneStatus status, StringView message, void* userdata1, void* userdata2) {
        HandleTasksFinished(status, userdata1);
    }

    public override void Wait<T>(GpuBuffer<T> buffer)
    {
        // var task = (WgpuTask)buffer.LastWritingTask;			TASK_TAG
        // if (task == null || task.IsCompleted) return;

        // We register a callback for completion
        bool completed = false;
        queue.OnSubmittedWorkDone(0, (QueueWorkDoneStatus status) => {
            completed = true;
        });

        while (!completed) {
            // Poll() triggers the internal event loop of WebGPU. This enables calling the callback above (in the same thread)
            // Poll(wait: true);
            wgpuInstanceProcessEvents(instance);
        }
    }
        
    private Buffer* CreateBufferWithData<T>(T[] data, BufferUsage usage, string bufferLabel) where T : unmanaged
    {
        uint    size            = (uint)(data.Length * sizeof(T));
        
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
        
        // Copy data into mapped memory
        void* pMapped = wgpuBufferGetMappedRange(buffer, 0, size);
        fixed (void* pData = data)
        {
            System.Buffer.MemoryCopy(pData, pMapped, size, size);
        }
        // Important: WebGPU has to unmap before GPU can use memory
        wgpuBufferUnmap(buffer); // initiate copy data to an extern GPU. returns immediately. Upload executes async. 
        
        return buffer;
    }
    
    private Buffer* CreateBuffer(uint size, BufferUsage usage, ReadOnlySpan<char> bufferLabel)
    {
        int     labelMaxCount   = WgpuUtils.GetMaxCount(bufferLabel);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        var len = WgpuUtils.CopySpanToBuffer(bufferLabel, labelBuffer, labelMaxCount);
        
        var desc = new BufferDescriptor {
            label           = WgpuUtils.FromPtrLength(labelBuffer, len),
            size            = size,
            usage           = (ulong)usage,
            mappedAtCreation = WgpuUtils.FromBool(false) // buffer is initially empty / unmapped
        };
        var buffer = wgpuDeviceCreateBuffer(DevicePtr, &desc);
        if (buffer == null) {
            throw new Exception("GPU memory allocation failed! Insufficient VRAM or incorrect alignment");
        }
        return buffer;
    }
    
    private Buffer* CreateStagingBuffer(uint size, ReadOnlySpan<char> bufferLabel)
    {
        int     labelMaxCount   = WgpuUtils.GetMaxCount(bufferLabel);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        var len = WgpuUtils.CopySpanToBuffer(bufferLabel, labelBuffer, labelMaxCount);
        
        var desc = new BufferDescriptor {
            label           = WgpuUtils.FromPtrLength(labelBuffer, len),
            size            = size,
            usage           = (ulong)(BufferUsage.CopyDst | BufferUsage.MapRead), // CopyDst | MapRead => staging buffer
            mappedAtCreation = WgpuUtils.FromBool(false)
        };
        var buffer = wgpuDeviceCreateBuffer(DevicePtr, &desc);
        if (buffer == null) {
            throw new Exception("GPU memory allocation failed! Insufficient VRAM or incorrect alignment");
        }
        return buffer;
    }
    
    private static BufferUsage FromGpuBufferUsage(GpuBufferUsage usage)
    {
        return
            ((usage & GpuBufferUsage.MapRead)       != 0 ? BufferUsage.MapRead      : BufferUsage.None) |
            ((usage & GpuBufferUsage.MapWrite)      != 0 ? BufferUsage.MapWrite     : BufferUsage.None) |
            ((usage & GpuBufferUsage.CopySrc)       != 0 ? BufferUsage.CopySrc      : BufferUsage.None) |
            ((usage & GpuBufferUsage.CopyDst)       != 0 ? BufferUsage.CopyDst      : BufferUsage.None) |
            ((usage & GpuBufferUsage.Index)         != 0 ? BufferUsage.Index        : BufferUsage.None) |
            ((usage & GpuBufferUsage.Vertex)        != 0 ? BufferUsage.Vertex       : BufferUsage.None) |
            ((usage & GpuBufferUsage.Uniform)       != 0 ? BufferUsage.Uniform      : BufferUsage.None) |
            ((usage & GpuBufferUsage.Storage)       != 0 ? BufferUsage.Storage      : BufferUsage.None) |
            ((usage & GpuBufferUsage.Indirect)      != 0 ? BufferUsage.Indirect     : BufferUsage.None) |
            ((usage & GpuBufferUsage.QueryResolve)  != 0 ? BufferUsage.QueryResolve : BufferUsage.None);
    }
    
    public override GpuLimits GetDeviceLimits()
    {
        var limits = new Limits();
        wgpuDeviceGetLimits(DevicePtr, &limits);
        return new GpuLimits {
            MaxStorageBufferBindingSize         = limits.maxStorageBufferBindingSize,  
            MaxComputeWorkgroupStorageSize      = limits.maxComputeWorkgroupStorageSize, 
            MaxBindGroups                       = limits.maxBindGroups, 
            MaxComputeInvocationsPerWorkgroup   = limits.maxComputeInvocationsPerWorkgroup, 
        };
    }
    
    public override GpuBuffer<T> CreateBuffer<T>(int length, GpuBufferUsage usage, string bufferLabel)
    {
        var wgpuUsage       = FromGpuBufferUsage(usage);
        var sizeInBytes     = (uint)(length * Unsafe.SizeOf<T>());
        var buffer          = CreateBuffer(sizeInBytes, wgpuUsage, bufferLabel);
        var stagingHandle   = CreateStagingBuffer(sizeInBytes, bufferLabel);
        var array           = new T[length];
        var gpuBuffer       = new WgpuBuffer<T>(this, buffer, bufferEntries.Count, stagingHandle, array, bufferLabel);
        bufferEntries.Add(new BufferEntry(gpuBuffer));
        return gpuBuffer;
    }
    
    public override GpuBuffer<T> CreateBuffer<T>(T[] data, GpuBufferUsage usage, string bufferLabel)
    {
        var wgpuUsage       = FromGpuBufferUsage(usage);
        var sizeInBytes     = (uint)(data.Length * Unsafe.SizeOf<T>());
        var handle          = CreateBufferWithData(data, wgpuUsage, bufferLabel);
        var stagingHandle   = CreateStagingBuffer(sizeInBytes, bufferLabel);
        var gpuBuffer       = new WgpuBuffer<T>(this, handle, bufferEntries.Count, stagingHandle, data, bufferLabel);
        bufferEntries.Add(new BufferEntry(gpuBuffer));
        return gpuBuffer;
    }
    
    private readonly    List<BufferRange>   tempRanges    = new();
    private readonly    List<BufferData>    activeBuffers = new ();
    
    public override void Download()
    {
        var requestedRanges = Recorder.requestedRanges;
        foreach (var range in requestedRanges) {
            bufferEntries[range.bufferId].requestedRanges.Add(range);
        }
        
        var encoder = wgpuDeviceCreateCommandEncoder(DevicePtr, null);
        activeBuffers.Clear();

        foreach (var bufferEntry in bufferEntries)
        {
            if (bufferEntry.requestedRanges.Count == 0) {
                continue;
            }
            var buffer              = bufferEntry.wgpuBuffer.GetBufferData();
            buffer.requestedRanges  = bufferEntry.requestedRanges;
            activeBuffers.Add(buffer);

            var  optimizedRanges = BufferRange.GetOptimizedRanges(bufferEntry.requestedRanges, tempRanges);
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

        // finish commands and send to GOU queue
        var commandBuffer = wgpuCommandEncoderFinish(encoder, null);
        wgpuQueueSubmit(QueuePtr, 1, &commandBuffer);
        
        wgpuCommandBufferRelease(commandBuffer);
        wgpuCommandEncoderRelease(encoder);

        int remainingMaps = activeBuffers.Count; // decremented to 0 if all wgpuBufferMapAsync are finished
        
        foreach (var buffer in activeBuffers)
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
            wgpuInstanceProcessEvents(instance);
        }
        // direct CPU -> CPU transfer staging memory -> host memory
        foreach (var buffer in activeBuffers)
        {
            uint totalBufferSizeInBytes = (uint)(buffer.length * buffer.elementSize);
            void* pMapped = wgpuBufferGetMappedRange(buffer.stagingHandle, 0, totalBufferSizeInBytes);
            buffer.wgpu.ExecuteCpuCopy(pMapped, buffer.requestedRanges);    // copy staging memory to host memory
            wgpuBufferUnmap(buffer.stagingHandle);                          // unmap so CPU is able to access
            buffer.requestedRanges.Clear();
        }
        activeBuffers.Clear();
    }
    
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    internal static void BufferMap_callback(MapAsyncStatus status, StringView message, void* userdata1, void* userdata2) {
        if (userdata1== null) return;
        var remainingMaps = (int*)userdata1;
        Interlocked.Decrement(ref *remainingMaps);
    }

    // ----------------------------- section "pure" methods used to create WebGPU structs ----------------------------- 
    public WgpuShaderModule CreateShaderModule(ReadOnlySpan<byte> wgslSource, ReadOnlySpan<byte> shaderLabel)
    {
        fixed (byte* pShaderBytes = wgslSource)
        fixed (byte* labelPtr = shaderLabel)
        {
            // create descriptor
            var wgslDesc = new ShaderSourceWGSL {    	// was: new ShaderModuleWGSLDescriptor
                code    = WgpuUtils.FromPtrSpan(pShaderBytes, wgslSource),
                chain   = new ChainedStruct {
                    sType   = SType.ShaderSourceWGSL	// was: SType.ShaderModuleWgsldescriptor
                }
            };
            var desc = new ShaderModuleDescriptor {
                label       = WgpuUtils.FromPtrSpan(labelPtr, shaderLabel),
                nextInChain = (ChainedStruct*)&wgslDesc,
            };
            // Compile shader in driver
            var handle = wgpuDeviceCreateShaderModule(DevicePtr, &desc);
            errorHandler.ThrowOnError();
            return new WgpuShaderModule(handle);
        }
    }
    
    public WgpuComputePipeline CreateComputePipeline(
        WgpuShaderModule    module,
        WgpuBindGroupLayout bufferLayout,
        WgpuBindGroupLayout uniformLayout,
        ReadOnlySpan<byte>  entryPoint)
    {
        Span<WgpuBindGroupLayout> layouts = stackalloc WgpuBindGroupLayout[2];
        layouts[0] = bufferLayout;
        layouts[1] = uniformLayout;
        
        fixed (byte*                pEntryPoint = entryPoint)
        fixed (WgpuBindGroupLayout*  layoutsPtr  = layouts)
        {
            var label = WgpuUtils.FromPtrSpan(pEntryPoint, entryPoint);
            var layoutDesc = new PipelineLayoutDescriptor {
                label                   = label,
                bindGroupLayoutCount    = 2,
                bindGroupLayouts        = (BindGroupLayout**)layoutsPtr
            };
            var pipelineLayout = wgpuDeviceCreatePipelineLayout(DevicePtr, &layoutDesc);
            try {
                var computeDesc = new ComputePipelineDescriptor {
                    label       = label,
                    layout      = pipelineLayout,
                    compute     = new ComputeState {	// was: new ProgrammableStageDescriptor
                        module      = module.handle,
                        entryPoint  = WgpuUtils.FromPtrSpan(pEntryPoint, entryPoint)
                    }
                };
                var handle = wgpuDeviceCreateComputePipeline(DevicePtr, &computeDesc);
                return new WgpuComputePipeline(handle);
            } finally {
                if (pipelineLayout != null) wgpuPipelineLayoutRelease(pipelineLayout);
                if (module.handle  != null) wgpuShaderModuleRelease(module.handle);
            }
        }
    }
    

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WgpuBindGroupLayout GetBindGroupLayout(ulong hashKey) {
        var cache = layoutCache;
        for (int n =  0; n < layoutCacheCount; n++) {
            if (hashKey == cache[n].hashKey) {
                return cache[n].layout;
            }
        }
        return default;
    }

    public WgpuBindGroupLayout CreateBindGroupLayout(ReadOnlySpan<WgpuLayoutEntry> entries, ulong hashKey, ReadOnlySpan<byte> layoutLabel)
    {
        Span<BindGroupLayoutEntry> nativeEntries = stackalloc BindGroupLayoutEntry[entries.Length];
        
        for (int i = 0; i < entries.Length; i++) {
            nativeEntries[i] = new BindGroupLayoutEntry {
                binding         = (uint)entries[i].Binding,
                visibility      = (ulong)ShaderStage.Compute,
                buffer          = new BufferBindingLayout {
                    type                = entries[i].Type,
                    hasDynamicOffset    = WgpuUtils.FromBool(false),    // default
                    minBindingSize      = 0                             // 0: no validation of minimum size
                }
            };
        }
        fixed (byte*                    labelPtr    = layoutLabel)
        fixed (BindGroupLayoutEntry*    entriesPtr  = nativeEntries)
        {
            var desc = new BindGroupLayoutDescriptor {
                label       = WgpuUtils.FromPtrSpan(labelPtr, layoutLabel),
                entryCount  = (uint)nativeEntries.Length,
                entries     = entriesPtr,
            };
            var handle = wgpuDeviceCreateBindGroupLayout(DevicePtr, &desc);
            if (handle == null)
                throw new Exception("Failed to create BindGroupLayout. Check your Slot-indexes!");
            
            // Add new GpuBindGroupLayout to layoutSlots
            var cache = layoutCache;
            if (layoutCacheCount >= cache.Length) {
                var newCache = new CachedGroupLayout[layoutCacheCount];
                Array.Copy(cache, newCache, cache.Length);
                cache = layoutCache = newCache;
            }
            var layout = new WgpuBindGroupLayout(handle);
            cache[layoutCacheCount++] = new CachedGroupLayout { hashKey = hashKey, layout = layout };
            return layout;
        }
    }
}

