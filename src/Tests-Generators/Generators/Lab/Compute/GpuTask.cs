using System;
using System.Threading.Tasks;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;


namespace Tests.Generators.Lab;

public sealed unsafe class GpuTask : IDisposable
{
    // High-performance static instance for bypass modes (Scalar/SIMD)
    public static readonly GpuTask Completed = new GpuTask(true);

    private readonly bool       _isStatic;
    private          bool       _isCompleted;
    
    // Pure native pointers - zero overhead
    private readonly Device*    _device;
    private readonly Wgpu*      _wgpuApi; 

    // Constructor for real GPU work
    internal GpuTask(GpuContext context)
    {
        _wgpuApi = context.WgpuPtr;
        _device = context.DevicePtr;
        _isStatic = false;
        _isCompleted = false;
    }

    // Constructor for the static Completed singleton
    private GpuTask(bool isStatic)
    {
        _isStatic = isStatic;
        _isCompleted = true;
        _device = null;
        _wgpuApi = null;
    }

    /// <summary>
    /// Forcibly blocks the CPU thread until the GPU signals completion.
    /// </summary>
    public void Wait()
    {
        if (_isCompleted || _isStatic || _device == null || _wgpuApi == null) 
            return;

        // Note: In WebGPU native, we poll the device. 
        // True = wait for work, False = just check status.
        while (!_isCompleted)
        {
            // Direct call to the native function pointer via Silk.NET
            _wgpuApi->DevicePoll(_device, true, null);
            _isCompleted = true; 
        }
    }

    /// <summary>
    /// Provides an awaitable bridge for the sync-started GPU work.
    /// </summary>
    public ValueTask Completion()
    {
        if (_isCompleted) return ValueTask.CompletedTask;
        
        // Prototype hack: Offload the blocking wait to a thread pool task
        return new ValueTask(Task.Run(Wait));
    }

    public void Dispose()
    {
        if (_isStatic) return;
        // In a real scenario, you might release specific task-related fences here.
    }
}