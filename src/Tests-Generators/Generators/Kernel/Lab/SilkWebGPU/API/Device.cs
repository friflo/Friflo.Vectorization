// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Friflo.Vectorization.GPU;
using Kernel.SilkWebGPU.Runtime;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;
using Buffer = Silk.NET.WebGPU.Buffer;
using Webgpu = Silk.NET.WebGPU.WebGPU;

// ReSharper disable InconsistentNaming
// ReSharper disable SwapViaDeconstruction
// ReSharper disable once CheckNamespace
namespace Kernel.SilkWebGPU;

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
//  - Non-Blocking Dependency Tracking: Automatic GPU task synchronization via a "Last Writing Task" mechanism
// Resource & Thread Management     
//  - Thread-Safe Command Dispatch      Designed for multithreaded environments
//  - Low-Overhead Resource Pooling     Efficient "Rent/Return" patterns for Tasks and Buffers to maintain a fixed memory footprint
//  - Type-Safe Buffer Abstraction      GpuBuffer<T> system bridges the gap between managed C# types and raw GPU memory.
// Developer Ergonomics
//  - Lean Codebase                     less than 40 KB minimizing instruction cache misses
//  - Compile-Time Safety               Heavy use of generics and constraints to catch errors at compile time / IDE
public sealed unsafe class SilkDevice : GpuDevice
{
    private             bool                isDisposed;
    public   override   ComputeMode         DefaultComputeMode  => ComputeMode.GPU;
    public   override   PipelineContext     PipelineContext     => null;
    public   override   bool                IsDisposed          => isDisposed;
    internal readonly   Webgpu              wgpu;
    private  readonly   Wgpu                wgpuEx;
    internal            Device*             DevicePtr   { get; } 
    internal            Queue*              QueuePtr    { get; }
        
    private  readonly   SilkTask[]          taskPool;
    private  readonly   Stack<SilkTask>     availableTasks;
    internal readonly   SilkBuffer<byte>    globalUniformPool;      // Each task uses its own slice from this pool
    private  readonly   SilkQueue           queue;
    
    private static      int                 effectSlotCount;
    private             SilkEffect[]        effectSlots  	= new SilkEffect[4];
    private             List<SilkTask>      pendingTasks    = new(1024);
    private             List<SilkTask>      inFlightTasks   = new(1024);
    private             GCHandle            deviceHandle;
    private readonly    void*               deviceHandlePtr;
    
    private static      int                 layoutCacheCount;
    private             CachedGroupLayout[] layoutCache  = new CachedGroupLayout[64];

    // --- pointers to callback methods
    private static  readonly    PfnQueueWorkDoneCallback    WorkDoneCallback = PfnQueueWorkDoneCallback.From(HandleTasksFinished);

    // Every class implementing IDispose must follow the same pattern. Set GpuInstance code sample.
    public override void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this); // prevent execution of finalizer WHEN Dispose() is called manually
    }
    
    // A finalizer can be call from any thread.
    ~SilkDevice() {
        Dispose(false); // false: release only native pointers
    }

    private void Dispose(bool disposing)
    {
        if (isDisposed) return;  // guarantees this block is executed only once

        // Other managed objects MUST not be touched if disposing == false.
        if (disposing) {
            // case: only manual Dispose() call
            globalUniformPool?.Dispose();
            // TODO dispose taskPool, pendingTasks & GpuEffect
            
            if (DevicePtr != null) {
                if (QueuePtr != null) {
                    Flush(wait: true); // flush all pending GPU operations
                    wgpuEx.DevicePoll(DevicePtr, true, null); // "Drain callbacks" ensure no WorkDoneCallback's are called by polling all pending callbacks
                }
                wgpu.DeviceSetUncapturedErrorCallback(DevicePtr, callback: default, null); // release callback before device
            }
        }
        // Native resources cleanup - cases: manual Dispose() call & finalizer calls
        // Release native resources. Order matters: first queue than device
        // Native pointer MUST be checked for null. Their creation may have failed
        
        for (int n = 0; n < effectSlots.Length; n++) {
            ref var effect = ref effectSlots[n];
            effect.bufferCache.Release(wgpu);
            if(effect.IsCreated) {
                if (effect.pipeline.handle != null) wgpu.ComputePipelineRelease(effect.pipeline.handle);
            }
        }
        var cache = layoutCache;
        for (int n = 0; n < cache.Length; n++) {
            if (cache[n].layout.IsCreated) wgpu.BindGroupLayoutRelease(cache[n].layout.handle);
            cache[n] = default;
        }
        // Important: Queue* must not be released. It shares the same lifetime as Device*.
        //  if (QueuePtr != null) {
        //      wgpu.QueueRelease(QueuePtr); will cause segtfault/panic when calling wgpu.QueueSubmit()
        //  }
        if (DevicePtr != null) {
            wgpu.DeviceRelease(DevicePtr);
        }
        // Free anchor to managed world MUST be the last call 
        if (deviceHandle.IsAllocated) {
            deviceHandle.Free();
        }
        isDisposed = true;
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SilkTask RentTask() {
        lock (availableTasks) {
            return availableTasks.Pop();
        }
    }

    private void ReturnTask(SilkTask task)
    {
        task.Reset();
        lock (availableTasks) {
            availableTasks.Push(task);
        }
    }
    
    public GpuBuffer<T> RentBuffer<T>(int inputLength) where T : unmanaged
    {
        throw new NotImplementedException();
    }
    
    // --- effectSlots
    // NewGpuEffectSlot() is called only once per shadow method. It stores the slot index in a static readonly int
    public static int NewEffectSlot() => Interlocked.Increment(ref effectSlotCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SilkEffect GetEffect(int slot) {
        var slots = effectSlots;
        if (slot < slots.Length) {
            return slots[slot];
        }
        return default;
    }
    
    public ref SilkEffect CreateEffect(
        int                 slot,
        SilkComputePipeline  pipeline,
        SilkBindGroupLayout bufferLayout,
        SilkBindGroupLayout uniformLayout)
    {
        var slots = effectSlots;
        if (slot >= slots.Length) {
            var newSlots = new SilkEffect[effectSlotCount];
            Array.Copy(slots, newSlots, slots.Length);
            slots = effectSlots = newSlots;
        }
        slots[slot] = new SilkEffect(pipeline, bufferLayout, uniformLayout);
        return ref slots[slot];
    }
    
    public void UpdateBufferCache(int slot, SilkBindGroup bindGroup, ulong hash) {
        effectSlots[slot].bufferCache.Update(wgpu, bindGroup, hash);
    }

    internal SilkDevice(
        Webgpu              wgpu,
        Wgpu                wgpuEx,
        string              label,
        Device*             devicePtr,
        Queue*              queuePtr,
        int                 maxTasks,
        int                 slotSize)
    : base(label, slotSize)
    {
        this.wgpu           = wgpu;    
        this.wgpuEx         = wgpuEx;
        DevicePtr           = devicePtr;
        QueuePtr            = queuePtr;
        queue               = new SilkQueue(this, queuePtr);
        deviceHandle        = GCHandle.Alloc(this);
        deviceHandlePtr     = (void*)GCHandle.ToIntPtr(deviceHandle);
        
        globalUniformPool   = (SilkBuffer<byte>)CreateBuffer<byte>(maxTasks * slotSize, "globalUniformPool", BufferProfile.StaticIn, BufferType.Uniform);
        taskPool            = new SilkTask[maxTasks];
        availableTasks      = new Stack<SilkTask>(maxTasks);
        for (int i = 0; i < maxTasks; i++) {
            var task = new SilkTask(this, i);
            taskPool[i] = task;
            availableTasks.Push(task);
        }
    }
    
    public void Poll(bool wait) {
        wgpuEx.DevicePoll(DevicePtr, true, null);
    }

    internal SilkEncoder CreateEncoder(SilkTask task, ReadOnlySpan<byte> encoderLabel)
    {
        fixed (byte* labelPtr = encoderLabel)
        {
            var desc = new CommandEncoderDescriptor {
                Label = labelPtr
            };
            var encoder = wgpu.DeviceCreateCommandEncoder(DevicePtr, &desc);
            return new SilkEncoder(task, encoder);
        }
    }

    internal void WriteBuffer<T>(SilkBuffer<T> buffer, uint byteOffset, void* data, uint byteSize) where T : unmanaged {
        queue.WriteBuffer(buffer.handle, byteOffset, data, byteSize);
    }
    
    // -------------------------------- Task Dependency Tracking --------------------------------
    private static void HandleTasksFinished(QueueWorkDoneStatus status, void* userData)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)userData);
        if (handle.Target is SilkDevice device) {
            device.ReturnPendingTasks();
        }
    }
    
    private void ReturnPendingTasks() {
         // Be ultra safe. DevicePoll() in Dispose(disposing) should already ensure HandleTasksFinished() is not fired anymore
        if (isDisposed) return; 
        for (int i = 0; i < inFlightTasks.Count; i++) {
            var task = inFlightTasks[i];
            ReturnTask(task);
        }
        inFlightTasks.Clear();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(SilkTask task)
    {
        pendingTasks.Add(task);
        if (pendingTasks.Count >= 1024) { 
            Flush(); // ensure list does not grow unlimited
        }
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public override void Flush(bool wait = true)
    {
        var tasks = pendingTasks;
        int count = tasks.Count;
        if (count == 0 && !wait) return;
        
        // Is previous batch already send?
        while (inFlightTasks.Count > 0) {
            wgpuEx.DevicePoll(DevicePtr, true, null); // forces "work done" callback
        }
        
        if (count > 0) {
            // Submit command buffers to queue
            var commandBuffers = stackalloc CommandBuffer*[tasks.Count];
            for (int n = 0; n < tasks.Count; n++) {
                commandBuffers[n] = tasks[n].commandBuffer;
            }
            wgpu.QueueSubmit(queue.handle, (uint)tasks.Count, commandBuffers);
            
            // Swap list references
            var temp        = inFlightTasks;
            inFlightTasks   = tasks;
            pendingTasks    = temp;
            
            // Register callback for the new In-Flight batch
            wgpu.QueueOnSubmittedWorkDone(queue.handle, WorkDoneCallback, deviceHandlePtr);
        }
        // If deterministic result is required, wait until the current batch finishes
        if (wait) {
            while (inFlightTasks.Count > 0) {
                wgpuEx.DevicePoll(DevicePtr, true, null);
            }
        }
    }
    
    public void WaitInDebug()
    {
        if (!DebugMode) {
            return;
        }
        Flush();
    }

    // TODO - remove - kept temporary for reference
    private void Wait<T>(GpuBuffer<T> buffer) where T : unmanaged
    {
        // if (task == null || task.IsCompleted) return;
        var completed = false;

        // We register a callback for the specific task completion
        queue.OnSubmittedWorkDone(0, (QueueWorkDoneStatus status) => {
            completed = true;
        });

        while (!completed) {
            // Poll() triggers the internal event loop of WebGPU. This enables calling the callback above (in the same thread)
            Poll(wait: true); 
        }
    }
    
    private readonly List<ISilkBuffer> requestedBuffers = [];
    
    public void TrackWrite<T>(in Buffer<T> buffer) where T : unmanaged
    {
        requestedBuffers.Add((ISilkBuffer)buffer.GpuBuffer);
    }
    
    // not efficient but enables use of the same Download() API
    public override void Download()
    {
        foreach (var buffer in requestedBuffers) {
            buffer.Download();
        }
        requestedBuffers.Clear();
    }
        
    private Buffer* CreateBufferWithData<T>(T[] data, BufferUsage usage, string bufferLabel) where T : unmanaged
    {
        uint    size            = (uint)(data.Length * sizeof(T));
        
        int     labelMaxCount   = SilkUtils.GetMaxCount(bufferLabel);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        SilkUtils.CopySpanToBuffer(bufferLabel, labelBuffer, labelMaxCount);
        
        var desc = new BufferDescriptor {
            Label           = labelBuffer,
            Size            = size,
            Usage           = usage | BufferUsage.CopyDst,  // CopyDst to write data into
            MappedAtCreation = true                         // We want to write now
        };
        var buffer = wgpu.DeviceCreateBuffer(DevicePtr, &desc);
        
        // Copy data into mapped memory
        void* pMapped = wgpu.BufferGetMappedRange(buffer, 0, size);
        fixed (void* pData = data)
        {
            System.Buffer.MemoryCopy(pData, pMapped, size, size);
        }
        // Important: WebGPU has to unmap before GPU can use memory
        wgpu.BufferUnmap(buffer);
        
        return buffer;
    }
    
    private Buffer* CreateBuffer(uint size, BufferUsage usage, ReadOnlySpan<char> bufferLabel)
    {
        int     labelMaxCount   = SilkUtils.GetMaxCount(bufferLabel);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        SilkUtils.CopySpanToBuffer(bufferLabel, labelBuffer, labelMaxCount);
        
        var desc = new BufferDescriptor {
            Label           = labelBuffer,
            Size            = size,
            Usage           = usage,
            MappedAtCreation = false // buffer is initially empty / unmapped
        };
        var buffer = wgpu.DeviceCreateBuffer(DevicePtr, &desc);
        if (buffer == null) {
            throw new Exception("GPU memory allocation failed! Insufficient VRAM or incorrect alignment");
        }
        return buffer;
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
    
    public override GpuLimits GetDeviceLimits()
    {
        var supportedLimits = new SupportedLimits();
        wgpu.DeviceGetLimits(DevicePtr, &supportedLimits);
        var limits = supportedLimits.Limits;
        return new GpuLimits {
            MaxStorageBufferBindingSize         = (long)limits.MaxStorageBufferBindingSize,  
            MaxComputeWorkgroupStorageSize      = (int) limits.MaxComputeWorkgroupStorageSize, 
            MaxBindGroups                       = (int) limits.MaxBindGroups, 
            MaxComputeInvocationsPerWorkgroup   = (int) limits.MaxComputeInvocationsPerWorkgroup, 
        };
    }
    
    public override GpuBuffer<T> CreateBuffer<T>(int length, string bufferLabel, BufferProfile profile, BufferType type = BufferType.Storage)
    {
        var wgpuUsage   = GetBufferUsage(profile, type);
        var sizeInBytes = length * Unsafe.SizeOf<T>();
        var buffer      = CreateBuffer((uint)sizeInBytes, wgpuUsage, bufferLabel);
        var array       = new T[length];
        return new SilkBuffer<T>(this, buffer, array, bufferLabel);
    }
    
    public override GpuBuffer<T> CreateBuffer<T>(T[] data, string bufferLabel, BufferProfile profile, BufferType type = BufferType.Storage)
    {
        var wgpuUsage   = GetBufferUsage(profile, type);
        var handle      = CreateBufferWithData(data, wgpuUsage, bufferLabel);
        return new SilkBuffer<T>(this, handle, data, bufferLabel);
    }

    // ----------------------------- section "pure" methods used to create WebGPU structs ----------------------------- 
    public SilkShaderModule CreateShaderModule(ReadOnlySpan<byte> wgslSource, ReadOnlySpan<byte> shaderLabel)
    {
        fixed (byte* pShaderBytes = wgslSource)
        fixed (byte* labelPtr = shaderLabel)
        {
            // create descriptor
            var wgslDesc = new ShaderModuleWGSLDescriptor {
                Code        = pShaderBytes,
                Chain       = new ChainedStruct {
                    SType       = SType.ShaderModuleWgsldescriptor
                }
            };
            var desc = new ShaderModuleDescriptor {
                Label       = labelPtr,
                NextInChain = (ChainedStruct*)&wgslDesc,
            };
            // Compile shader in driver
            var handle = wgpu.DeviceCreateShaderModule(DevicePtr, &desc);
            return new SilkShaderModule(handle);
        }
    }
    
    public SilkComputePipeline CreateComputePipeline(
        SilkShaderModule    module,
        SilkBindGroupLayout bufferLayout,
        SilkBindGroupLayout uniformLayout,
        ReadOnlySpan<byte>  entryPoint)
    {
        Span<SilkBindGroupLayout> layouts = stackalloc SilkBindGroupLayout[2];
        layouts[0] = bufferLayout;
        layouts[1] = uniformLayout;
        
        fixed (byte*                pEntryPoint = entryPoint)
        fixed (SilkBindGroupLayout*  layoutsPtr  = layouts)
        {
            var layoutDesc = new PipelineLayoutDescriptor {
                Label                   = pEntryPoint,
                BindGroupLayoutCount    = 2,
                BindGroupLayouts        = (BindGroupLayout**)layoutsPtr
            };
            var pipelineLayout = wgpu.DeviceCreatePipelineLayout(DevicePtr, &layoutDesc);
            try {
                var computeDesc = new ComputePipelineDescriptor {
                    Layout      = pipelineLayout,
                    Compute     = new ProgrammableStageDescriptor {
                        Module      = module.handle,
                        EntryPoint  = pEntryPoint
                    }
                };
                var handle = wgpu.DeviceCreateComputePipeline(DevicePtr, &computeDesc);
                return new SilkComputePipeline(handle);
            } finally {
                if (pipelineLayout != null) wgpu.PipelineLayoutRelease(pipelineLayout);
                if (module.handle  != null) wgpu.ShaderModuleRelease(module.handle);
            }
        }
    }
    

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SilkBindGroupLayout GetBindGroupLayout(ulong hashKey) {
        var cache = layoutCache;
        for (int n =  0; n < layoutCacheCount; n++) {
            if (hashKey == cache[n].hashKey) {
                return cache[n].layout;
            }
        }
        return default;
    }

    public SilkBindGroupLayout CreateBindGroupLayout(Span<SilkLayoutEntry> entries, ulong hashKey, ReadOnlySpan<byte> layoutLabel)
    {
        Span<BindGroupLayoutEntry> nativeEntries = stackalloc BindGroupLayoutEntry[entries.Length];
        
        for (int i = 0; i < entries.Length; i++) {
            nativeEntries[i] = new BindGroupLayoutEntry {
                Binding         = (uint)entries[i].Binding,
                Visibility      = ShaderStage.Compute,
                Buffer          = new BufferBindingLayout {
                    Type                = entries[i].Type,
                    HasDynamicOffset    = false,        // default
                    MinBindingSize      = 0             // 0: no validation of minimum size
                }
            };
        }
        fixed (byte*                    labelPtr    = layoutLabel)
        fixed (BindGroupLayoutEntry*    entriesPtr  = nativeEntries)
        {
            var desc = new BindGroupLayoutDescriptor {
                Label       = labelPtr,
                EntryCount  = (uint)nativeEntries.Length,
                Entries     = entriesPtr,
            };
            var handle = wgpu.DeviceCreateBindGroupLayout(DevicePtr, &desc);
            if (handle == null)
                throw new Exception("Failed to create BindGroupLayout. Check your Slot-indexes!");
            
            // Add new GpuBindGroupLayout to layoutSlots
            var cache = layoutCache;
            if (layoutCacheCount >= cache.Length) {
                var newCache = new CachedGroupLayout[layoutCacheCount];
                Array.Copy(cache, newCache, cache.Length);
                cache = layoutCache = newCache;
            }
            var layout = new SilkBindGroupLayout(handle);
            cache[layoutCacheCount++] = new CachedGroupLayout { hashKey = hashKey, layout = layout };
            return layout;
        }
    }
}

