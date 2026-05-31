// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;


/// <summary>Defines how native GPU compute/render passes are batched and managed.</summary>
public enum PassBatching
{
    /// <summary>No optimization; forces a new native pass for every single kernel call.</summary>
    None,

    /// <summary>Automatic optimization; defers and merges passes based on data hazards on-the-fly.</summary>
    HazardDriven,

    /// <summary>Manual control; passes are managed explicitly by calling <see cref="PipelineContext.NewPass"/>.</summary>
    Manual
}

[DebuggerTypeProxy(typeof(PipelineContextDebugView))]
public class PipelineContext : IDisposable
{
    private readonly    GpuDevice   device; 
    internal            int         ownerThreadId;
    internal            string      callerFile;
    internal            int         callerLine;
    
    public virtual  PassBatching                    PassBatching        { get; set; }
    public virtual  bool                            EnableTraces        { get; set; }
    
    public          string                          TraceLog            => AppendTraceLog (new StringBuilder()).ToString();
    public          string                          KernelMetricLog     => AppendMetricLog(new StringBuilder()).ToString();
    public          PipelineStats                   Stats               => GetStats();
    public          ReadOnlySpan<PipelineTrace>     Traces              => GetTraces();
    public          ReadOnlySpan<KernelMetric>      KernelMetrics       => GetKernelMetrics();
    
    public virtual  void                            ClearTraces()       { }
    public virtual  void                            ClearKernelMetrics(){ }
    
    public virtual  void                            NewPass()           { }
    
    public virtual  void                            Download()          { }
    public virtual  void                            Flush()             { }
    public virtual  void                            Synchronize()       { }
    
    protected virtual  PipelineStats                GetStats()          => default;
    protected virtual  ReadOnlySpan<PipelineTrace>  GetTraces()         => default;
    protected virtual  ReadOnlySpan<KernelMetric>   GetKernelMetrics()  => default;
    
    public  override    string                      ToString()          => AppendToString(new StringBuilder()).ToString();
    public  virtual     void                        Dispose()           { }
    
    private sealed class PipelineContextDebugView(PipelineContext context)
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly PipelineContext context = context;

        public  PassBatching    PassBatching    { get => context.PassBatching;  set =>  context.PassBatching = value;  }
        public  bool            EnableTraces    { get => context.EnableTraces;  set =>  context.EnableTraces = value;  }
        public  PipelineStats   Stats           => context.Stats;
        public  PipelineTrace[] Traces          => context.Traces.ToArray();
        public  KernelMetric[]  KernelMetrics   => context.KernelMetrics.ToArray();
    }
    
    protected internal PipelineContext(GpuDevice device) {
        this.device = device;
    }

    private StringBuilder AppendToString(StringBuilder sb)
    {
        var stats = GetStats();
        sb.Append($"batching: {PassBatching}  calls: {stats.Calls}   passes: {stats.Passes}  hazards: {stats.Hazards}");
        return sb;
    }

    private StringBuilder AppendTraceLog(StringBuilder sb)
    {
        sb.Append("--- PIPELINE TRACE (");
        AppendToString(sb);
        sb.Append(") ---");
            
        if (PassBatching == PassBatching.HazardDriven) {
            sb.Append("\n--- Lock-free GPU kernels with deferred, on-the-fly hazard-driven pass batching");
        }
        foreach (var trace in GetTraces()) {
            sb.Append('\n');
            trace.Append(sb, 29);
        }
        return sb;
    }
    
    private StringBuilder AppendMetricLog(StringBuilder sb)
    {
        sb.Append($"--- KERNEL METRIC ---");
        var array = GetKernelMetrics().ToArray();
        Array.Sort(array);
        foreach (var metric in array) {
            if (metric.Calls == 0) {
                continue;   
            }
            var name    = metric.KernelName;
            var len     = Math.Max(0, 29 - name.Length);
            sb.Append($"\n{metric.KernelName}()").Append(' ',len).Append($" calls: {metric.Calls}  passes: {metric.Passes}");
        }
        return sb;
    }
    
    // --------------------------------------- threading ---------------------------------------

    internal void Initialize(int threadId)
    {
        ownerThreadId = threadId;
    }

    internal void Reset()
    {
        ownerThreadId = -1;
        if (device.DebugMode) {
            callerFile = null;
            callerLine = 0;
        }
        // TODO rest internal resources / offsets
    }
    
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)] [StackTraceHidden]
    protected void ValidateThreadSafety()
    {
        if (ownerThreadId != Environment.CurrentManagedThreadId) {
            ThrowInvalidThread();
        }
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)] [StackTraceHidden] [DoesNotReturn]
    internal void ThrowInvalidThread()
    {
        var name = Thread.CurrentThread.Name ?? "unknown thread";
        throw new InvalidOperationException(
        $"[Thread Context Violation] method executes on thread: {Environment.CurrentManagedThreadId} ({name}) but PipelineContext belongs to thread {ownerThreadId}!");
    }
}
