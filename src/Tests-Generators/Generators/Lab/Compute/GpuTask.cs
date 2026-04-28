using System;
using System.Threading.Tasks;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

namespace Tests.Generators.Lab;

public sealed class GpuTask : IDisposable
{
    // High-performance static instance for bypass modes (Scalar/SIMD)
    public static readonly GpuTask Completed = new GpuTask(true);

    private readonly bool       _isStatic;
    private          bool       _isCompleted;
    
    // Pure native pointers - zero overhead
    private readonly GpuContext _ctx;

    // Constructor for real GPU work
    internal GpuTask(GpuContext context)
    {
        _ctx = context;
        _isStatic = false;
        _isCompleted = false;
    }

    // Constructor for the static Completed singleton
    private GpuTask(bool isStatic)
    {
        _isStatic = isStatic;
        _isCompleted = true;
    }

    /// <summary>
    /// Forcibly blocks the CPU thread until the GPU signals completion.
    /// </summary>
    public unsafe void Wait()
    {
        if (_isCompleted || _isStatic) 
            return;

        // Note: In WebGPU native, we poll the device. 
        // True = wait for work, False = just check status.
        while (!_isCompleted)
        {
            // Direct call to the native function pointer via Silk.NET

            _ctx.Poll();
            _isCompleted = true; 
        }
    }

    /// <summary>
    /// Provides an awaitable bridge for the sync-started GPU work.
    /// </summary>
    public async Task Completion()
    {
        // In einer echten Implementierung würdest du hier ein Callback von WebGPU abwarten.
        // Bis dahin hilft oft eine Schleife, die den Status prüft:
        while(!_isCompleted) {
            // _ctx.Poll(); // Ruft intern wgpuDevicePoll auf
            await Task.Yield(); 
        }
    }

    public void Dispose()
    {
        if (_isStatic) return;
        // In a real scenario, you might release specific task-related fences here.
    }
}