using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tests.Generators.Lab;

public sealed class GpuTask : IDisposable
{
    // High-performance static instance for bypass modes (Scalar/SIMD)
    public static readonly GpuTask Completed = new GpuTask(true);

    private readonly bool       _isStatic;
    // The actual recorded commands
    public GpuCommandBuffer? Commands { get; internal set; }
    
    // Tasks that MUST finish before this one starts
    private readonly List<GpuTask> _dependencies = new();
    
    // A simple state flag for the scheduler
    public bool IsSubmitted { get; internal set; }
    public bool IsCompleted { get; internal set; }
    
    // Pure native pointers - zero overhead
    private readonly GpuContext _ctx;

    // Constructor for real GPU work
    internal GpuTask(GpuContext context)
    {
        _ctx = context;
        _isStatic = false;
    }
    
    internal void Reset() {
        Commands = null;
        _dependencies.Clear();
        IsSubmitted = false;
        IsCompleted = false;
    }

    // Constructor for the static Completed singleton
    private GpuTask(bool isStatic)
    {
        _isStatic = isStatic;
        IsCompleted = true;
    }
    
    public void AddDependency(GpuTask predecessor) {
        if (predecessor == this) return; // Prevent brain-loop
        if (!_dependencies.Contains(predecessor))
        {
            _dependencies.Add(predecessor);
        }
    }

    /// <summary>
    /// Forcibly blocks the CPU thread until the GPU signals completion.
    /// </summary>
    public void Wait()
    {
        // Note: In WebGPU native, we poll the device. 
        // True = wait for work, False = just check status.
        while (!IsCompleted) {
            // Direct call to the native function pointer via Silk.NET
            _ctx.Poll(wait: true);
        }
        // IsCompleted is set by GpuContext to 'true' when GPU-Callback fires!
    }

    /// <summary>
    /// Provides an awaitable bridge for the sync-started GPU work.
    /// </summary>
    public async Task Completion()
    {
        // In einer echten Implementierung würdest du hier ein Callback von WebGPU abwarten.
        // Bis dahin hilft oft eine Schleife, die den Status prüft:
        while(!IsCompleted) {
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