// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU.Runtime;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

// ReSharper disable SwapViaDeconstruction
namespace Friflo.Vectorization.GPU;

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
public sealed unsafe class GpuDevice : IDisposable
{
    private  readonly   string          label;
    private             bool            isDisposed;
    public              bool            IsDisposed => isDisposed;
    internal readonly   WebGPU          wgpu;
    private  readonly   Wgpu            wgpuEx;
    internal            Device*         DevicePtr   { get; } 
    internal            Queue*          QueuePtr    { get; }
    
    public              bool            DebugMode   { get; set; } 
    
    internal readonly   int             slotSize;
    private  readonly   GpuTask[]       taskPool;
    private  readonly   Stack<GpuTask>  availableTasks;
    internal readonly   GpuBuffer<byte> globalUniformPool;      // Each task uses its own slice from this pool
    private  readonly   GpuQueue        queue;
    
    private static      int             gpuEffectSlotCount = 0;
    private             GpuEffect[]     gpuEffectSlots  = new GpuEffect[4];
    private             List<GpuTask>   pendingTasks    = new(1024);
    private             List<GpuTask>   inFlightTasks   = new(1024);
    private             GCHandle        deviceHandle;
    private readonly    void*           deviceHandlePtr;

    public  override    string          ToString() => label + (isDisposed ? ": Disposed" : ": Alive");

    // --- pointers to callback methods
    private static  readonly    PfnQueueWorkDoneCallback    WorkDoneCallback = PfnQueueWorkDoneCallback.From(HandleTasksFinished);

    // Every class implementing IDispose must follow the same pattern. Set GpuInstance code sample.
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this); // prevent execution of finalizer WHEN Dispose() is called manually
    }
    
    // A finalizer can be call from any thread.
    ~GpuDevice() {
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
        
        foreach(var effect in gpuEffectSlots) {
            if(effect.IsCreated) {
                if (effect.pipeline.handle      != null) wgpu.ComputePipelineRelease(effect.pipeline.handle);
                if (effect.bufferLayout.handle  != null) wgpu.BindGroupLayoutRelease(effect.bufferLayout.handle);
                if (effect.uniformLayout.handle != null) wgpu.BindGroupLayoutRelease(effect.uniformLayout.handle);
            }
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
    public GpuTask RentTask() {
        lock (availableTasks) {
            return availableTasks.Pop();
        }
    }

    private void ReturnTask(GpuTask task)
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
    
    // NewGpuEffectSlot() is called only once per shadow method. It stores the slot index in a static readonly int  
    public static int NewGpuEffectSlot() => gpuEffectSlotCount++;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GpuEffect GetEffect(int slot) {
        var slots = gpuEffectSlots;
        if (slot < slots.Length) {
            return slots[slot];    
        }
        return default;
    }
    
    public ref GpuEffect CreateEffect(
        int                 slot,
        GpuComputePipeline  pipeline,
        GpuBindGroupLayout  uniformLayout)
    {
        var slots = gpuEffectSlots;
        if (slot >= slots.Length) {
            var newSlots = new GpuEffect[gpuEffectSlotCount];
            Array.Copy(slots, newSlots, slots.Length);
            slots = gpuEffectSlots = newSlots;
        }
        slots[slot] = new GpuEffect(pipeline, uniformLayout);
        return ref slots[slot];
    }

    internal GpuDevice(
        WebGPU              wgpu,
        Wgpu                wgpuEx,
        string              label,
        Device*             devicePtr,
        Queue*              queuePtr,
        int                 maxTasks,
        int                 slotSize)
    {
        this.wgpu           = wgpu;    
        this.wgpuEx         = wgpuEx;
        this.label          = label;
        DevicePtr           = devicePtr;
        QueuePtr            = queuePtr;
        queue               = new GpuQueue(this, queuePtr);
        this.slotSize       = slotSize;
        
        deviceHandle        = GCHandle.Alloc(this);
        deviceHandlePtr     = (void*)GCHandle.ToIntPtr(deviceHandle);
        
        globalUniformPool   = new GpuBuffer<byte>(this, (uint)(maxTasks * slotSize), BufferUsage.Uniform | BufferUsage.CopyDst, "globalUniformPool");
        taskPool            = new GpuTask[maxTasks];
        availableTasks      = new Stack<GpuTask>(maxTasks);
        for (int i = 0; i < maxTasks; i++) {
            var task = new GpuTask(this, i);
            taskPool[i] = task;
            availableTasks.Push(task);
        }
    }
    
    public void Poll(bool wait) {
        wgpuEx.DevicePoll(DevicePtr, true, null);
    }

    internal GpuEncoder CreateEncoder(GpuTask task, ReadOnlySpan<byte> encoderLabel)
    {
        fixed (byte* labelPtr = encoderLabel)
        {
            var desc = new CommandEncoderDescriptor {
                Label = labelPtr
            };
            var encoder = wgpu.DeviceCreateCommandEncoder(DevicePtr, &desc);
            return new GpuEncoder(task, encoder);
        }
    }

    internal void WriteBuffer<T>(GpuBuffer<T> buffer, uint byteOffset, void* data, uint byteSize) where T : unmanaged {
        queue.WriteBuffer(buffer.handle, byteOffset, data, byteSize);
    }
    
    // -------------------------------- Task Dependency Tracking --------------------------------
    private static void HandleTasksFinished(QueueWorkDoneStatus status, void* userData)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)userData);
        if (handle.Target is GpuDevice device) {
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
    public void Enqueue(GpuTask task)
    {
        pendingTasks.Add(task);
        if (pendingTasks.Count >= 1024) { 
            Flush(); // ensure list does not grow unlimited
        }
    }
    
    public void Flush(bool wait = true)
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

    public void Wait<T>(GpuBuffer<T> buffer) where T : unmanaged
    {
        var task = buffer.LastWritingTask;
        if (task == null || task.IsCompleted) return;

        // We register a callback for the specific task completion
        queue.OnSubmittedWorkDone(0, (QueueWorkDoneStatus status) => {
            task.IsCompleted = true;
        });

        while (!task.IsCompleted) {
            // Poll() triggers the internal event loop of WebGPU. This enables calling the callback above (in the same thread)
            Poll(wait: true); 
        }
    }
        
    public unsafe void SubmitGraph(GpuTask finalTask)
    {
        // 1. Flatten the tree (Breadth-First or Depth-First Search)
        // To find the correct execution order (Topological Sort)
        var executionOrder = SortTasks(finalTask);

        // 2. Submit them in order
        foreach (var task in executionOrder)
        {
            if (task.IsSubmitted) continue;
            
            // Every task in WebGPU within the same Queue is 
            // guaranteed to start in submission order.
            var ptr = task.commandBuffer;
            wgpu.QueueSubmit(QueuePtr, 1, &ptr);
            
            task.IsSubmitted = true;
        }
    }

    private IEnumerable<GpuTask> SortTasks(GpuTask finalTask)
    {
        throw new NotImplementedException();
    }
    
    public Buffer* CreateBufferWithData<T>(T[] data, BufferUsage usage, string label) where T : unmanaged
    {
        uint    size            = (uint)(data.Length * sizeof(T));
        
        int     labelMaxCount   = GpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        GpuUtils.CopySpanToBuffer(label, labelBuffer, labelMaxCount);
        
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
    
    internal Buffer* CreateBuffer(uint size, BufferUsage usage, ReadOnlySpan<char> label)
    {
        int     labelMaxCount   = GpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        GpuUtils.CopySpanToBuffer(label, labelBuffer, labelMaxCount);
        
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

    // ----------------------------- section "pure" methods used to create WebGPU structs ----------------------------- 
    public GpuShaderModule CreateShaderModule(ReadOnlySpan<byte> wgslSource, ReadOnlySpan<byte> shaderLabel)
    {
        fixed (byte* pShaderBytes = wgslSource)
        fixed (byte* labelPtr = shaderLabel)
        {
            // create descriptor
            var wgslDesc = new ShaderModuleWGSLDescriptor {
                Code        = pShaderBytes,
                Chain       = new ChainedStruct {
                    SType       = SType.ShaderModuleWgsldescriptor // Wichtig: SType definiert den Inhalt
                }
            };
            var desc = new ShaderModuleDescriptor {
                Label       = labelPtr,
                NextInChain = (ChainedStruct*)&wgslDesc,
            };
            // Compile shader in driver
            var handle = wgpu.DeviceCreateShaderModule(DevicePtr, &desc);
            return new GpuShaderModule(handle);
        }
    }
    
    public GpuComputePipeline CreateComputePipeline(
        GpuShaderModule     module,
        GpuBindGroupLayout  bufferLayout,
        GpuBindGroupLayout  uniformLayout,
        ReadOnlySpan<byte>  entryPoint)
    {
        Span<GpuBindGroupLayout> layouts = stackalloc GpuBindGroupLayout[2];
        layouts[0] = bufferLayout;
        layouts[1] = uniformLayout;
        
        fixed (byte*                pEntryPoint = entryPoint)
        fixed (GpuBindGroupLayout*  layoutsPtr  = layouts)
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
                return new GpuComputePipeline(handle);
            } finally {
                if (pipelineLayout != null) wgpu.PipelineLayoutRelease(pipelineLayout);
                if (module.handle  != null) wgpu.ShaderModuleRelease(module.handle);
            }
        }
    }

    public GpuBindGroupLayout CreateBindGroupLayout(Span<GpuLayoutEntry> entries, ReadOnlySpan<byte> layoutLabel)
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

            return new GpuBindGroupLayout(handle);
        }
    }
}

