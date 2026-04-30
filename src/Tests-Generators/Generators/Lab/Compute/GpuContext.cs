// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

// ReSharper disable SwapViaDeconstruction
namespace Friflo.Vectorization.GPU;

public sealed unsafe class GpuContext : IDisposable
{
    internal            WebGPU          wgpu        { get; }    // main API         - GpuContext owns this managed type
    private             Wgpu            wgpuEx      { get; }    // extension (Poll) - GpuContext owns this managed type
    internal            Device*         DevicePtr   { get; }    // pointer lives in graphics device driver 
    internal            Queue*          QueuePtr    { get; }    // pointer lives in graphics device driver
    private             Instance*       Instance    { get; }    // pointer lives in graphics device driver
    
    public              bool            DebugMode   { get; set; } 
    
    private readonly    GpuTask[]       taskPool;
    private readonly    Stack<GpuTask>  availableTasks;
    
    private static      int             gpuEffectSlotCount = 0;
    private             GpuEffect[]     gpuEffectSlots = new GpuEffect[4];
    private             List<GpuTask>   pendingTasks = new(1024);
    private             List<GpuTask>   inFlightTasks = new(1024);
    private             GCHandle        contextHandle;
    private readonly    void*           contextHandlePtr;
 
    
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
    
    public static int NewGpuEffectSlot() => gpuEffectSlotCount++; 

    public GpuEffect GetGpuEffect(int slot) {
        var slots = gpuEffectSlots;
        if (slot < slots.Length) {
            return slots[slot];    
        }
        return null;
    }
    
    public void SetGpuEffect(int slot, GpuEffect gpuEffect) {
        var slots = gpuEffectSlots;
        if (slot >= slots.Length) {
            var newSlots = new GpuEffect[gpuEffectSlotCount];
            Array.Copy(slots, newSlots, slots.Length);
            slots = gpuEffectSlots = newSlots;
        }
        slots[slot] = gpuEffect;
    }
    
    // ReSharper disable once NotAccessedField.Local
    private readonly PfnErrorCallback errorCallback; // must ensure callback is not collected by GC

    private GpuContext(
        WebGPU          wgpu,
        Wgpu            wgpuEx,
        Device*         devicePtr,
        Queue*          queuePtr,
        Instance*       instance,
        PfnErrorCallback errorCallback,
        int             maxConcurrentTasks)
    {
        this.wgpu           = wgpu;    
        this.wgpuEx         = wgpuEx;
        DevicePtr           = devicePtr;
        QueuePtr            = queuePtr;
        Instance            = instance;
        queue               = new GpuQueue(this, queuePtr);
        this.errorCallback  = errorCallback;
        
        contextHandle      = GCHandle.Alloc(this);
        contextHandlePtr   = (void*)GCHandle.ToIntPtr(contextHandle);
        
        uniformPool = new GpuBuffer<byte>(this, 64 * 1024, BufferUsage.Uniform | BufferUsage.CopyDst); // or 256 * 1024
        
        taskPool            = new GpuTask[maxConcurrentTasks];
        availableTasks      = new Stack<GpuTask>(maxConcurrentTasks);
        for (int i = 0; i < maxConcurrentTasks; i++) {
            var task = new GpuTask(this, i);
            taskPool[i] = task;
            availableTasks.Push(task);
        }
    }
    
    public static GpuContext Create(int maxConcurrentTasks = 64)
    {
        var wgpu = WebGPU.GetApi();
        if (!wgpu.TryGetDeviceExtension(null, out Wgpu wgpuEx)) {
            throw new Exception("WGPU extension not found!");
        }
		// 1. Instanz & Surface (optional, für Compute reicht oft der Adapter)
		// Wir holen uns den Adapter (die physische GPU)
		InstanceDescriptor instDesc = new InstanceDescriptor();
		var instance = wgpu.CreateInstance(&instDesc);

		// 2. Adapter anfordern
		Adapter* adapter = null;
		var options = new RequestAdapterOptions { 
			PowerPreference = PowerPreference.HighPerformance 
		};

		// WebGPU ist hier asynchron, wir müssen auf den Callback warten
		wgpu.InstanceRequestAdapter(instance, &options, PfnRequestAdapterCallback.From((status, adp, _, _) => {
			if (status == RequestAdapterStatus.Success) adapter = adp;
		}), null);

		// Warten, bis der Adapter da ist (dafür brauchen wir die Extension!)
		if (!wgpu.TryGetDeviceExtension(null, out wgpuEx)) {
			throw new Exception("WGPU extension not found!");
		}
        while (adapter == null) {
            wgpu.InstanceProcessEvents(instance); 
        }

		// 3. Device anfordern
		Device* device = null;
        var name = Marshal.StringToHGlobalAnsi("GpuContext");
		var devDesc = new DeviceDescriptor {
			Label = (byte*)name
		};

		wgpu.AdapterRequestDevice(adapter, &devDesc, PfnRequestDeviceCallback.From((status, dev, _, _) => {
			if (status == RequestDeviceStatus.Success) device = dev;
		}), null);

        while (device == null) {
            wgpu.InstanceProcessEvents(instance); 
        }
        Marshal.FreeHGlobal(name); // after device is set is safe to release. name is consumed async  


		// 4. Pointer setzen

		var queuePtr = wgpu.DeviceGetQueue(device);
        
        var errorCallback = PfnErrorCallback.From(OnGpuError);
        wgpu.DeviceSetUncapturedErrorCallback(device, errorCallback, null);
        
        return new GpuContext(wgpu, wgpuEx,  device, queuePtr, instance, errorCallback, maxConcurrentTasks);
    }

    private static void OnGpuError(ErrorType type, byte* message, void* userData) {
        string errorMsg = Marshal.PtrToStringAnsi((IntPtr)message);
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine("--- [WEBGPU CRITICAL ERROR] ---");
        Console.Error.WriteLine($"Type: {type}");
        Console.Error.WriteLine($"Message: {errorMsg}");
        Console.Error.WriteLine("-------------------------------");
        Console.ResetColor();
        if (Debugger.IsAttached) Debugger.Break();
    }
    
    public void Poll(bool wait) 
    {
        wgpuEx.DevicePoll(DevicePtr, true, null);
    }

    public void Dispose()
    {
        uniformPool?.Dispose();
        if (contextHandle.IsAllocated) contextHandle.Free();
    
        if (QueuePtr  != null) wgpu.QueueRelease(QueuePtr);
        if (DevicePtr != null) wgpu.DeviceRelease(DevicePtr);
        
        wgpu.InstanceRelease(Instance);
    }

    internal GpuEncoder CreateEncoder(GpuTask task) {
        CommandEncoderDescriptor desc = new CommandEncoderDescriptor { Label = null };
        var encoder = wgpu.DeviceCreateCommandEncoder(DevicePtr, &desc);
        return new GpuEncoder(task, encoder);
    }

    public void Submit(GpuCommandBuffer commandBuffer) {
        var handle = commandBuffer.handle;
        // WebGPU erwartet ein Array von CommandBuffern
        wgpu.QueueSubmit(QueuePtr, 1, &handle);
        
        // Optional: Den Buffer releasen, wenn er nicht mehr gebraucht wird
        // wgpu.CommandBufferRelease(handle);                                       TODO
    }
    
    public GpuBindGroupLayoutBuilder BindGroupLayoutBuilder()
    {
        return new GpuBindGroupLayoutBuilder(this);
    }

    private readonly GpuBuffer<byte>    uniformPool;
    private uint                        poolOffset;
    
    public GpuBindEntry AsUniformEntry<T>(int binding, T value) where T : unmanaged
    {
        uint size           = (uint)sizeof(T);
        uint alignedOffset  = (poolOffset + 255) & ~255u;                      // WebGPU requires Uniform offset must by 256 byte aligned
        // Note: WriteBuffer() copies data. May use a Mapped Buffer in future for more performance
        WriteBuffer(uniformPool, alignedOffset, &value, size);                 // write value in uniformPool
        poolOffset = alignedOffset + size;
        return new GpuBindEntry(binding, uniformPool, alignedOffset, size);    // use uniformPool at alignedOffset
    }
    
    private GpuQueue queue;
    
    private void WriteBuffer<T>(GpuBuffer<T> buffer, uint byteOffset, void* data, uint byteSize) where T : unmanaged
    {
        queue.WriteBuffer(
            buffer.handle,
            byteOffset,        // offset in buffer
            data,              // pointer on my value
            byteSize           // value size
        );
    }

    public void ResetPool() => poolOffset = 0; // Am Ende des Frames/Batches rufen
    
    // ------------------- Task Dependency Tracking
    
    private static readonly PfnQueueWorkDoneCallback WorkDoneCallback = PfnQueueWorkDoneCallback.From(HandleTasksFinished);
    
    private static void HandleTasksFinished(QueueWorkDoneStatus status, void* userData)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)userData);
        if (handle.Target is GpuContext ctx) {
            ctx.ReturnPendingTasks();
        }
    }
    
    private void ReturnPendingTasks() {
        for (int i = 0; i < inFlightTasks.Count; i++) {
            var task = inFlightTasks[i];
            ReturnTask(task);
        }
        inFlightTasks.Clear();
    }
    
    public void Enqueue(GpuTask task)
    {
        pendingTasks.Add(task);
        if (pendingTasks.Count >= 1024) { 
            Flush(); // ensure list does not grow unlimited
        }
    }
    
    public void Flush(bool wait = true)
    {
        int count = pendingTasks.Count;
        if (count == 0 && !wait) return;
        
        // Is previous batch already send?
        while (inFlightTasks.Count > 0) {
            wgpuEx.DevicePoll(DevicePtr, true, null); // forces "work done" callback
        }
        
        if (count > 0) {
            // Submit command buffers to queue
            var tasks = pendingTasks;
            var commandBuffers = stackalloc CommandBuffer*[tasks.Count];
            for (int n = 0; n < tasks.Count; n++) {
                commandBuffers[n] = pendingTasks[n].CommandBuffer!.handle;
            }
            wgpu.QueueSubmit(queue.Handle, (uint)tasks.Count, commandBuffers);
            
            // Swap list references
            var temp        = inFlightTasks;
            inFlightTasks  = pendingTasks;
            pendingTasks   = temp;
            
            // Register callback for the new In-Flight batch
            wgpu.QueueOnSubmittedWorkDone(queue.Handle, WorkDoneCallback, contextHandlePtr);
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
        ResetPool();
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
            var ptr = task.CommandBuffer!.handle;
            wgpu.QueueSubmit(QueuePtr, 1, &ptr);
            
            task.IsSubmitted = true;
        }
    }

    private IEnumerable<GpuTask> SortTasks(GpuTask finalTask)
    {
        throw new NotImplementedException();
    }
    
    public Buffer* CreateBufferWithData<T>(T[] data, BufferUsage usage) where T : unmanaged
    {
        uint size = (uint)(data.Length * sizeof(T));
        
        var desc = new BufferDescriptor {
            Size = size,
            Usage = usage | BufferUsage.CopyDst, // CopyDst to write data into
            MappedAtCreation = true              // We want to write now
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
    
    public Buffer* CreateBuffer(uint size, BufferUsage usage)
    {
        var desc = new BufferDescriptor
        {
            Size = size,
            Usage = usage,
            MappedAtCreation = false // Der Buffer ist initial leer/ungemappt
        };

        var buffer = wgpu.DeviceCreateBuffer(DevicePtr, &desc);
        
        if (buffer == null) {
            throw new Exception("GPU Memory Allocation failed! Zu wenig VRAM oder falsches Alignment?");
        }

        return buffer;
    }

    public GpuShaderModule CreateShaderModule(ReadOnlySpan<byte> wgslSource)
    {
        fixed (byte* pShaderBytes = wgslSource)
        {
            // create descriptor
            var wgslDesc = new ShaderModuleWGSLDescriptor {
                Code = pShaderBytes,
                Chain = new ChainedStruct {
                    SType = SType.ShaderModuleWgsldescriptor // Wichtig: SType definiert den Inhalt
                }
            };
            var desc = new ShaderModuleDescriptor {
                NextInChain = (ChainedStruct*)&wgslDesc,
                Label = null // Hier könnte ein Name für Debugger stehen
            };
            // Compile shader in driver
            var handle = wgpu.DeviceCreateShaderModule(DevicePtr, &desc);
            return new GpuShaderModule(handle);
        }
    }
    
    public GpuComputePipeline CreateComputePipeline(GpuShaderModule module, string entryPoint, GpuBindGroupLayout layout)
    {
        var pipelineLayout = CreatePipelineLayout(layout);
        byte[] entryPointBytes = Encoding.UTF8.GetBytes(entryPoint);
        fixed (byte* pEntryPoint = entryPointBytes)
        {
            var desc = new ComputePipelineDescriptor
            {
                Layout = pipelineLayout,
                Compute = new ProgrammableStageDescriptor {
                    Module = module.handle,
                    EntryPoint = pEntryPoint
                }
            };
            var handle = wgpu.DeviceCreateComputePipeline(DevicePtr, &desc);
            return new GpuComputePipeline(handle, pipelineLayout);
        }
    }
    
    private PipelineLayout* CreatePipelineLayout(GpuBindGroupLayout layout)
    {
        var layoutHandle = layout.handle;
        var desc = new PipelineLayoutDescriptor {
            BindGroupLayoutCount = 1,
            BindGroupLayouts = &layoutHandle
        };
        return wgpu.DeviceCreatePipelineLayout(DevicePtr, &desc);
    }
}

public sealed class GpuEffect 
{
    public required GpuBindGroupLayout Layout { get; init; }
    public required GpuComputePipeline Pipeline { get; init; }
}

public sealed unsafe class GpuComputePipeline
{
    internal readonly ComputePipeline* handle;
    internal readonly PipelineLayout*  layout;
    
    internal GpuComputePipeline(ComputePipeline* handle, PipelineLayout* layout) {
        this.handle = handle;
        this.layout = layout;
    }
}


public sealed unsafe class GpuShaderModule
{
    internal readonly ShaderModule* handle;
    
    internal GpuShaderModule(ShaderModule* handle) {
        this.handle = handle;    
    }
}
