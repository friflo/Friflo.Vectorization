using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Tests.Generators.Lab;

public unsafe class GpuContext : IDisposable
{
    public  WebGPU  _wgpu       { get; }    // main API         - GpuContext owns this managed type
    private Wgpu    _wgpuEx;                // extension (Poll) - GpuContext owns this managed type
    public  Device* DevicePtr   { get; }    // pointer lives in graphics device driver 
    public  Queue*  QueuePtr    { get; }    // pointer lives in graphics device driver
    
    public bool DebugMode       { get; set; } 
    
    private readonly Stack<GpuTask> _taskPool = new();
    
    private GpuEffect[] gpuEffectSlots;
    
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
    
    
    public GpuEffect GetGpuEffect(int slot) {
        return gpuEffectSlots[slot];
    }
    
    public void SetGpuEffect(int slot, GpuEffect gpuEffect) {
        gpuEffectSlots[slot] = gpuEffect;
    }
    
    private readonly PfnErrorCallback _errorCallback; // must ensure callback is not collected by GC

    private GpuContext (WebGPU wgpu, Wgpu wgpuEx, Device* devicePtr, Queue*  queuePtr, PfnErrorCallback errorCallback)
    {
        _wgpu           = wgpu;    
        _wgpuEx         = wgpuEx;
        DevicePtr       = devicePtr;
        QueuePtr        = queuePtr;
        _errorCallback  = errorCallback;
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
        
        var _errorCallback = PfnErrorCallback.From(OnGpuError);
        _wgpu.DeviceSetUncapturedErrorCallback(device, _errorCallback, null);
        
        return new GpuContext(_wgpu, _wgpuEx,  device, queuePtr, _errorCallback);
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

    public void Dispatch(Buffer<byte> w, Buffer<float> i, float u) 
    {
        // feed CommandEncoder
    }

    public void Dispose() { /* Cleanup native resources */ }

    public GpuEncoder CreateEncoder() {
        throw new NotImplementedException();
    }

    public void Submit(GpuCommandBuffer commandBuffer) {
        throw new NotImplementedException();
    }
    
    public BindGroupLayoutBuilder BindGroupLayoutBuilder()
    {
        throw new NotImplementedException();
    }

    public GpuBindGroup CreateBindGroup(GpuBindGroupLayout layout, Span<GpuBindEntry> bindEntries)
    {
        // Allocate native entries on the stack (efficient, no GC pressure)
        var nativeEntries = stackalloc BindGroupEntry[bindEntries.Length];

        for (int i = 0; i < bindEntries.Length; i++)
        {
            nativeEntries[i] = new BindGroupEntry
            {
                Binding = bindEntries[i].Binding,
                // Direct handle to the native WGPUBuffer
                Buffer = (Silk.NET.WebGPU.Buffer*)bindEntries[i].BufferHandle, 
                // The byte offset (crucial for our Uniform Pool)
                Offset = bindEntries[i].Offset,
                // The byte size of the slice
                Size = bindEntries[i].Size
            };
        }

        // Prepare the descriptor for the native API call
        var descriptor = new BindGroupDescriptor {
            Layout = layout.Handle,
            EntryCount = (uint)bindEntries.Length,
            Entries = nativeEntries
        };
        BindGroup* handle = layout.context._wgpu.DeviceCreateBindGroup(DevicePtr, &descriptor);
        return new GpuBindGroup((IntPtr)handle);
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
            buffer.Handle,
            byteOffset,        // offset in buffer
            data,              // pointer on my value
            byteSize           // value size
        );
    }

    public void ResetPool() => _poolOffset = 0; // Am Ende des Frames/Batches rufen
    
    // ------------------- Task Dependency Tracking
    public void Enqueue(GpuTask task)
    {
        var cmdBuffer = task.FinalizeCommands(); // Only now a complete CommandBuffer is created from Encoder
        _queue.Submit(cmdBuffer); // submit to WebGPU
        // Optional: If we are in Cluster this is the place to prepare the message for the next node
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
            var ptr = (CommandBuffer*)task.CommandBuffer!.Handle;
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
    
    public unsafe PipelineLayout* CreatePipelineLayout(GpuBindGroupLayout layout)
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