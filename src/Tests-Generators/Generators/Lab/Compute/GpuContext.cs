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

public unsafe class GpuContext : IDisposable
{
    public              WebGPU      _wgpu       { get; }    // main API         - GpuContext owns this managed type
    internal            Wgpu        _wgpuEx     { get; }    // extension (Poll) - GpuContext owns this managed type
    public              Device*     DevicePtr   { get; }    // pointer lives in graphics device driver 
    public              Queue*      QueuePtr    { get; }    // pointer lives in graphics device driver
    public              Instance*   Instance    { get; }    // pointer lives in graphics device driver
    
    public  bool        DebugMode   { get; set; } 
    
    private readonly    Stack<GpuTask>  _taskPool = new(1024);
    
    private static      int             gpuEffectSlotCount = 0;
    private             GpuEffect[]     gpuEffectSlots = new GpuEffect[4];
    private             List<GpuTask>   _pendingTasks = new(1024);
    private             List<GpuTask>   _inFlightTasks = new(1024);
    private             GCHandle        _contextHandle;
    private             void*           _contextHandlePtr;
 
    
    public GpuTask RentTask()
    {
        if (_taskPool.TryPop(out var task)) return task;
        return new GpuTask(this); // Nur wenn der Pool leer ist, wird einmalig alloziert
    }

    public void ReturnTask(GpuTask task)
    {
        task.Reset(); // Wichtig: Alten State löschen!
        _taskPool.Push(task);
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
    
    private readonly PfnErrorCallback _errorCallback; // must ensure callback is not collected by GC

    private GpuContext (WebGPU wgpu, Wgpu wgpuEx, Device* devicePtr, Queue*  queuePtr, Instance* instance, PfnErrorCallback errorCallback)
    {
        _wgpu           = wgpu;    
        _wgpuEx         = wgpuEx;
        DevicePtr       = devicePtr;
        QueuePtr        = queuePtr;
        Instance        = instance;
        _queue          = new GpuQueue(this, queuePtr);
        _errorCallback  = errorCallback;
        
        _contextHandle      = GCHandle.Alloc(this);
        _contextHandlePtr   = (void*)GCHandle.ToIntPtr(_contextHandle);
        
        _uniformPool = new GpuBuffer<byte>(this, 64 * 1024, BufferUsage.Uniform | BufferUsage.CopyDst); // or 256 * 1024
    }
    
    public static GpuContext Create()
    {
        var _wgpu = WebGPU.GetApi();
        if (!_wgpu.TryGetDeviceExtension(null, out Wgpu _wgpuEx)) {
            throw new Exception("WGPU extension not found!");
        }
		// 1. Instanz & Surface (optional, für Compute reicht oft der Adapter)
		// Wir holen uns den Adapter (die physische GPU)
		InstanceDescriptor instDesc = new InstanceDescriptor();
		var instance = _wgpu.CreateInstance(&instDesc);

		// 2. Adapter anfordern
		Adapter* adapter = null;
		var options = new RequestAdapterOptions { 
			PowerPreference = PowerPreference.HighPerformance 
		};

		// WebGPU ist hier asynchron, wir müssen auf den Callback warten
		_wgpu.InstanceRequestAdapter(instance, &options, PfnRequestAdapterCallback.From((status, adp, msg, userData) => {
			if (status == RequestAdapterStatus.Success) adapter = adp;
		}), null);

		// Warten, bis der Adapter da ist (dafür brauchen wir die Extension!)
		if (!_wgpu.TryGetDeviceExtension(null, out _wgpuEx)) {
			throw new Exception("WGPU extension not found!");
		}
        while (adapter == null) {
            _wgpu.InstanceProcessEvents(instance); 
        }

		// 3. Device anfordern
		Device* device = null;
        var name = Marshal.StringToHGlobalAnsi("GpuContext");
		var devDesc = new DeviceDescriptor {
			Label = (byte*)name
		};

		_wgpu.AdapterRequestDevice(adapter, &devDesc, PfnRequestDeviceCallback.From((status, dev, msg, userData) => {
			if (status == RequestDeviceStatus.Success) device = dev;
		}), null);

        while (device == null) {
            _wgpu.InstanceProcessEvents(instance); 
        }
        Marshal.FreeHGlobal(name); // after device is set is safe to release. name is consumed async  


		// 4. Pointer setzen

		var queuePtr = _wgpu.DeviceGetQueue(device);
        
        var errorCallback = PfnErrorCallback.From(OnGpuError);
        _wgpu.DeviceSetUncapturedErrorCallback(device, errorCallback, null);
        
        return new GpuContext(_wgpu, _wgpuEx,  device, queuePtr, instance, errorCallback);
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
        _wgpuEx.DevicePoll(DevicePtr, true, null);
    }

    public void Dispose()
    {
        _uniformPool?.Dispose();
        if (_contextHandle.IsAllocated) _contextHandle.Free();
    
        if (QueuePtr  != null) _wgpu.QueueRelease(QueuePtr);
        if (DevicePtr != null) _wgpu.DeviceRelease(DevicePtr);
        
        _wgpu.InstanceRelease(Instance);
    }

    internal GpuEncoder CreateEncoder(GpuTask task) {
        CommandEncoderDescriptor desc = new CommandEncoderDescriptor { Label = null };
        var encoder = _wgpu.DeviceCreateCommandEncoder(DevicePtr, &desc);
        return new GpuEncoder(task, encoder);
    }

    public void Submit(GpuCommandBuffer commandBuffer) {
        var handle = commandBuffer.Handle;
        // WebGPU erwartet ein Array von CommandBuffern
        _wgpu.QueueSubmit(QueuePtr, 1, &handle);
        
        // Optional: Den Buffer releasen, wenn er nicht mehr gebraucht wird
        // _wgpu.CommandBufferRelease(handle);                                       TODO
    }
    
    public GpuBindGroupLayoutBuilder BindGroupLayoutBuilder()
    {
        return new GpuBindGroupLayoutBuilder(this);
    }

    private GpuBuffer<byte> _uniformPool;
    private uint            _poolOffset = 0;
    
    public GpuBindEntry AsUniformEntry<T>(int binding, T value) where T : unmanaged
    {
        uint size           = (uint)sizeof(T);
        uint alignedOffset  = (_poolOffset + 255) & ~255u;                      // WebGPU requires Uniform offset must by 256 byte aligned
        // Note: WriteBuffer() copies data. May use a Mapped Buffer in future for more performance
        WriteBuffer(_uniformPool, alignedOffset, &value, size);                 // write value in _uniformPool
        _poolOffset = alignedOffset + size;
        return new GpuBindEntry(binding, _uniformPool, alignedOffset, size);    // use _uniformPool at alignedOffset
    }
    
    private GpuQueue _queue;
    
    private void WriteBuffer<T>(GpuBuffer<T> buffer, uint byteOffset, void* data, uint byteSize) where T : unmanaged
    {
        _queue.WriteBuffer(
            buffer._handle,
            byteOffset,        // offset in buffer
            data,              // pointer on my value
            byteSize           // value size
        );
    }

    public void ResetPool() => _poolOffset = 0; // Am Ende des Frames/Batches rufen
    
    // ------------------- Task Dependency Tracking
    
    private static readonly PfnQueueWorkDoneCallback _workDoneCallback = PfnQueueWorkDoneCallback.From(HandleTasksFinished);
    
    private static void HandleTasksFinished(QueueWorkDoneStatus status, void* userData)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)userData);
        if (handle.Target is GpuContext ctx) {
            ctx.ReturnPendingTasks();
        }
    }
    
    private void ReturnPendingTasks() {
        for (int i = 0; i < _inFlightTasks.Count; i++) {
            var task = _inFlightTasks[i];
            ReturnTask(task);
        }
        _inFlightTasks.Clear();
    }
    
    public void Enqueue(GpuTask task)
    {
        _pendingTasks.Add(task);
        if (_pendingTasks.Count >= 1024) { 
            Flush(); // ensure list does not grow unlimited
        }
    }
    
    public void Flush(bool wait = true)
    {
        int count = _pendingTasks.Count;
        if (count == 0 && !wait) return;
        
        // Is previous batch already send?
        while (_inFlightTasks.Count > 0) {
            _wgpuEx.DevicePoll(DevicePtr, true, null); // forces "work done" callback
        }
        
        if (count > 0) {
            // Submit command buffers to queue
            var tasks = _pendingTasks;
            var commandBuffers = stackalloc CommandBuffer*[tasks.Count];
            for (int n = 0; n < tasks.Count; n++) {
                commandBuffers[n] = _pendingTasks[n].CommandBuffer!.Handle;
            }
            _wgpu.QueueSubmit(_queue.Handle, (uint)tasks.Count, commandBuffers);
            
            // Swap list references
            var temp        = _inFlightTasks;
            _inFlightTasks  = _pendingTasks;
            _pendingTasks   = temp;
            
            // Register callback for the new In-Flight batch
            _wgpu.QueueOnSubmittedWorkDone(_queue.Handle, _workDoneCallback, _contextHandlePtr);
        }
        // If deterministic result is required, wait until the current batch finishes
        if (wait) {
            while (_inFlightTasks.Count > 0) {
                _wgpuEx.DevicePoll(DevicePtr, true, null);
            }
        }
    }

    public void Wait<T>(GpuBuffer<T> buffer) where T : unmanaged
    {
        var task = buffer.LastWritingTask;
        if (task == null || task.IsCompleted) return;

        // We register a callback for the specific task completion
        _queue.OnSubmittedWorkDone(0, (QueueWorkDoneStatus status) => {
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
            var ptr = task.CommandBuffer!.Handle;
            _wgpu.QueueSubmit(QueuePtr, 1, &ptr);
            
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
        var buffer = _wgpu.DeviceCreateBuffer(DevicePtr, &desc);
        
        // Copy data into mapped memory
        void* pMapped = _wgpu.BufferGetMappedRange(buffer, 0, size);
        fixed (void* pData = data)
        {
            System.Buffer.MemoryCopy(pData, pMapped, size, size);
        }
        // Important: WebGPU has to unmap before GPU can use memory
        _wgpu.BufferUnmap(buffer);
        
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

        var buffer = _wgpu.DeviceCreateBuffer(DevicePtr, &desc);
        
        if (buffer == null) {
            throw new Exception("GPU Memory Allocation failed! Zu wenig VRAM oder falsches Alignment?");
        }

        return buffer;
    }

    public unsafe GpuShaderModule CreateShaderModule(string wgslSource)
    {
        byte[] shaderBytes = Encoding.UTF8.GetBytes(wgslSource);
        
        fixed (byte* pShaderBytes = shaderBytes)
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
            var handle = _wgpu.DeviceCreateShaderModule(DevicePtr, &desc);
            return new GpuShaderModule(handle);
        }
    }
    
    public unsafe GpuComputePipeline CreateComputePipeline(GpuShaderModule module, string entryPoint, GpuBindGroupLayout layout)
    {
        var pipelineLayout = CreatePipelineLayout(layout);
        byte[] entryPointBytes = Encoding.UTF8.GetBytes(entryPoint);
        fixed (byte* pEntryPoint = entryPointBytes)
        {
            var desc = new ComputePipelineDescriptor
            {
                Layout = pipelineLayout,
                Compute = new ProgrammableStageDescriptor {
                    Module = module.Handle,
                    EntryPoint = pEntryPoint
                }
            };
            var handle = _wgpu.DeviceCreateComputePipeline(DevicePtr, &desc);
            return new GpuComputePipeline(handle, pipelineLayout);
        }
    }
    
    public PipelineLayout* CreatePipelineLayout(GpuBindGroupLayout layout)
    {
        var layoutHandle = layout.Handle;
        var desc = new PipelineLayoutDescriptor {
            BindGroupLayoutCount = 1,
            BindGroupLayouts = &layoutHandle
        };
        return _wgpu.DeviceCreatePipelineLayout(DevicePtr, &desc);
    }
}

public class GpuEffect 
{
    public required GpuBindGroupLayout Layout { get; init; }
    public required GpuComputePipeline Pipeline { get; init; }
}

public unsafe class GpuComputePipeline
{
    internal readonly ComputePipeline* Handle;
    internal readonly PipelineLayout*  Layout;
    
    internal GpuComputePipeline(ComputePipeline* handle, PipelineLayout* layout) {
        Handle = handle;
        Layout = layout;
    }
}


public unsafe class GpuShaderModule
{
    internal readonly ShaderModule* Handle;
    
    internal GpuShaderModule(ShaderModule* handle) {
        Handle = handle;    
    }
}
