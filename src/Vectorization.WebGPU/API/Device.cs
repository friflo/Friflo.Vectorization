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
    
    internal readonly   WgpuBuffer<byte>    globalUniformPool;                                      // remove each CommandRecorder must have its own
    private  readonly   WgpuQueue           queue;
    
    private  static     int                 effectSlotCount;
    private             WgpuEffect[]        effectSlots  	= new WgpuEffect[4];
    private             GCHandle            deviceHandle;
    private  readonly   void*               deviceHandlePtr;
    
    private  readonly   BindGroupLayoutMap  layoutCache     = new ();
    internal readonly   List<IWgpuBuffer>   bufferMap       = [];
    internal readonly   CommandListPool     commandListPool = new ();
    
    private sealed class WgpuDeviceDebugView(WgpuDevice device)
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly WgpuDevice _device = device;

        public  string          Label       => _device.Label;
        public  bool            DebugMode   { get => _device.DebugMode;             set => _device.DebugMode            = value; }
        public  bool            IsDisposed  => _device.isDisposed;
        public  PipelineContext Context     => _device.Context;

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
            globalUniformPool?.Dispose();
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
        foreach (var layout in layoutCache.Values) {
            wgpuBindGroupLayoutRelease(layout.handle);
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
 
    // --- effectSlots
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
        
        globalUniformPool   = (WgpuBuffer<byte>)CreateBuffer<byte>(maxTasks * slotSize, 0, "globalUniformPool", BufferProfile.StaticIn, BufferType.Uniform);
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

    private int inFlightCommandBufferCount;
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public override void Submit()
    {
    }
    
    internal void SubmitCommandList(CommandList commandList)
    {
        var commands    = commandList.commands;
        int count       = commands.Count;
        if (count == 0) {
            return;
        }
        inFlightCommandBufferCount = 1;
        /* // Is previous batch already send?        
        
        while (Thread.VolatileRead(ref inFlightCommandBufferCount) > 0) {
            wgpuDevicePoll(DevicePtr, WgpuUtils.FromBool(true), null); // forces "work done" callback
        } */
        var commandBuffers = stackalloc CommandBuffer*[count];
        
        for (int n = 0; n < count; n++) {
            commandBuffers[n] = commands[n].handle;
        }
        commandListPool.Return(commandList);

        // Submit command buffers to queue
        wgpuQueueSubmit(queue.handle, (uint)count, commandBuffers);
        
        for (int n = 0; n < count; n++) {
            // Note: In case wgpuCommandEncoderFinish() detected a validation error
            //       releasing the handle will not decrement GpuHandleDiff.CommandBuffers
            wgpuCommandBufferRelease(commandBuffers[n]);
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
    
    public void WaitInDebug()
    {
        if (!DebugMode) {
            return;
        }
        Submit();
    }

    // TODO - remove - kept temporary for reference
    private void Wait<T>(GpuBuffer<T> buffer) where T : unmanaged
    {
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
    
    private Buffer* CreateBuffer(uint size, BufferUsage usage, ReadOnlySpan<char> bufferLabel)  // TODO remove
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
        var limits = new Limits();
        wgpuDeviceGetLimits(DevicePtr, &limits);
        return new GpuLimits {
            MaxStorageBufferBindingSize         = (long)limits.maxStorageBufferBindingSize,  
            MaxComputeWorkgroupStorageSize      = (int) limits.maxComputeWorkgroupStorageSize, 
            MaxBindGroups                       = (int) limits.maxBindGroups, 
            MaxComputeInvocationsPerWorkgroup   = (int) limits.maxComputeInvocationsPerWorkgroup, 
        };
    }
    
    public override GpuBuffer<T> CreateBuffer<T>(int length, T value, string bufferLabel, BufferProfile profile, BufferType type = BufferType.Storage)
    {
        var wgpuUsage       = GetBufferUsage(profile, type);
        var sizeInBytes     = (uint)(length * Unsafe.SizeOf<T>());
        var array           = new T[length];
        Array.Fill(array, value);
        var buffer          = CreateBufferWithData(array, wgpuUsage, bufferLabel);
        var stagingHandle   = CreateStagingBuffer(sizeInBytes, bufferLabel);
        var gpuBuffer       = new WgpuBuffer<T>(this, buffer, bufferMap.Count, stagingHandle, array, bufferLabel);
        bufferMap.Add(gpuBuffer);
        return gpuBuffer;
    }
    
    public override GpuBuffer<T> CreateBuffer<T>(T[] data, string bufferLabel, BufferProfile profile, BufferType type = BufferType.Storage)
    {
        var wgpuUsage       = GetBufferUsage(profile, type);
        var sizeInBytes     = (uint)(data.Length * Unsafe.SizeOf<T>());
        var handle          = CreateBufferWithData(data, wgpuUsage, bufferLabel);
        var stagingHandle   = CreateStagingBuffer(sizeInBytes, bufferLabel);
        var gpuBuffer       = new WgpuBuffer<T>(this, handle, bufferMap.Count, stagingHandle, data, bufferLabel);
        bufferMap.Add(gpuBuffer);
        return gpuBuffer;
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
    public WgpuBindGroupLayout GetBindGroupLayout(ulong hashKey)
    {
        layoutCache.TryGetValue(hashKey, out WgpuBindGroupLayout layout);
        return layout;
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
            
            // Add new GpuBindGroupLayout to cache
            var layout = new WgpuBindGroupLayout(handle);
            layoutCache.Add(hashKey, layout);
            return layout;
        }
    }
}

