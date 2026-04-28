using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tests.Generators.Lab;

public sealed class GpuTask : IDisposable
{
    private GpuEncoder? _currentEncoder;
    // The actual recorded commands
    public GpuCommandBuffer? CommandBuffer { get; internal set; }
    
    // Tasks that MUST finish before this one starts
    private readonly List<GpuTask> Dependencies = new();
    
    // A simple state flag for the scheduler
    public bool IsSubmitted { get; internal set; }
    public bool IsCompleted { get; internal set; }
    
    // Pure native pointers - zero overhead
    private readonly GpuContext _ctx;

    // Constructor for real GPU work
    internal GpuTask(GpuContext context)
    {
        _ctx = context;
    }
    
    // The task provides / owns the Encoder
    public GpuEncoder GetEncoder(GpuContext ctx) {
        return _currentEncoder ??= ctx.CreateEncoder();
    }
    
    // Before Task is pushed to Queue we Finish() _currentEncoder first  
    internal GpuCommandBuffer FinalizeCommands()
    {
        if (CommandBuffer != null) return CommandBuffer;
        
        if (_currentEncoder == null) throw new Exception("Task has no commands");
        
        CommandBuffer = _currentEncoder.Finish();
        _currentEncoder.Dispose(); // Encoder zurück in den Pool
        _currentEncoder = null;
        return CommandBuffer;
    }
    
    internal void Reset() {
        CommandBuffer?.Dispose();
        CommandBuffer = null;
        _currentEncoder = null; // was already Disposed()
        Dependencies.Clear();
        IsCompleted = false;
    }

    // Constructor for the static Completed singleton
    private GpuTask(bool isStatic)
    {
        IsCompleted = true;
    }
    
    public void AddDependency(GpuTask predecessor) {
        if (predecessor == this) return; // Prevent brain-loop
        if (!Dependencies.Contains(predecessor))
        {
            Dependencies.Add(predecessor);
        }
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
        // In a real scenario, you might release specific task-related fences here.
    }
}