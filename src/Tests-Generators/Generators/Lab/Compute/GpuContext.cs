using System;
using System.Collections.Generic;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

namespace Tests.Generators.Lab;

public unsafe class GpuContext : IDisposable
{
    public  WebGPU  _wgpu       { get; }    // main API         - GpuContext owns this managed type
    private Wgpu    _wgpuEx;                // extension (Poll) - GpuContext owns this managed type
    public  Device* DevicePtr   { get; }    // pointer lives in graphics device driver 
    public  Queue*  QueuePtr    { get; }    // pointer lives in graphics device driver
    
    public bool DebugMode       { get; set; } 
    
    private readonly Stack<GpuTask> _taskPool = new();
    
    private GpuBindGroupLayout[] bindGroupSlots;
    
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
    
    
    public GpuBindGroupLayout GetBindGroupLayout(int slot) {
        return bindGroupSlots[slot];
    }
    
    public void SetBindGroupLayout(int slot, GpuBindGroupLayout layout) {
        bindGroupSlots[slot] = layout;
    }

    public GpuContext()
    {
        _wgpu = WebGPU.GetApi();
        if (!_wgpu.TryGetDeviceExtension(null, out _wgpuEx)) {
            throw new Exception("WGPU extension not found!");
        }
        // _uniformPool = CreateBuffer<byte>(64 * 1024, BufferUsage.Uniform | BufferUsage.CopyDst);
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

    public GpuBatch BeginBatch() {
        return new GpuBatch();
    }

    public GpuEncoder CreateEncoder() {
        throw new NotImplementedException();
    }

    public void Submit(GpuCommandBuffer commandBuffer) {
        throw new NotImplementedException();
    }
    
    public GpuPipeline GetPipeline(string shaderName) {
        return new GpuPipeline();
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
        throw new NotImplementedException();
    }
    
    public void Wait<T>(GpuBuffer<T> buffer) where T : unmanaged {
        throw new NotImplementedException();
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
            var ptr = (CommandBuffer*)task.Commands!.Handle;
            _wgpu.QueueSubmit(QueuePtr, 1, &ptr);
            
            task.IsSubmitted = true;
        }
    }

    private IEnumerable<GpuTask> SortTasks(GpuTask finalTask)
    {
        throw new NotImplementedException();
    }
}