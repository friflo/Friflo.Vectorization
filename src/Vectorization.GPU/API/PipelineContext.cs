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
namespace Friflo.Vectorization.GPU.Runtime;

[DebuggerTypeProxy(typeof(PipelineContextDebugView))]
public class PipelineContext : IDisposable
{
    private readonly GpuDevice                      device; 
    
    public virtual  bool                            EnablePassBatching  { get; set; }
    public virtual  bool                            EnableTraces        { get; set; }
    
    public          string                          TraceLog            => AppendTraceLog (new StringBuilder()).ToString();
    public          string                          KernelMetricLog     => AppendMetricLog(new StringBuilder()).ToString();
    public          PipelineStats                   Stats               => GetStats();
    public          ReadOnlySpan<PipelineTrace>     Traces              => GetTraces();
    public          ReadOnlySpan<KernelMetric>      KernelMetrics       => GetKernelMetrics();
    
    public virtual  void                            ClearTraces()       { }
    public virtual  void                            ClearKernelMetrics(){ }
    
    protected virtual  PipelineStats                GetStats()          => default;
    protected virtual  ReadOnlySpan<PipelineTrace>  GetTraces()         => default;
    protected virtual  ReadOnlySpan<KernelMetric>   GetKernelMetrics()  => default;
    
    public  override    string                      ToString()          => AppendToString(new StringBuilder()).ToString();
    public  virtual     void                        Dispose()           { }
    
    private sealed class PipelineContextDebugView(PipelineContext context)
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly PipelineContext context = context;

        public  bool            EnablePassBatching  => context.EnablePassBatching;
        public  bool            EnableTraces        => context.EnableTraces;
        public  PipelineStats   Stats               => context.Stats;
        public  PipelineTrace[] Traces              => context.Traces.ToArray();
        public  KernelMetric[]  KernelMetrics       => context.KernelMetrics.ToArray();
    }
    
    protected internal PipelineContext(GpuDevice device) {
        this.device = device;
    }

    private StringBuilder AppendToString(StringBuilder sb)
    {
        var stats = GetStats();
        sb.Append($"batching: {EnablePassBatching}  calls: {stats.Calls}   passes: {stats.Passes}  hazards: {stats.Hazards}");
        return sb;
    }

    private StringBuilder AppendTraceLog(StringBuilder sb)
    {
        sb.Append("--- PIPELINE TRACE (");
        AppendToString(sb);
        sb.Append(") ---");
            
        if (EnablePassBatching) {
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
    
    internal            int                 OwnerThreadId        { get; set; }
    internal            string              AllocationStackTrace { get; set; }


    internal void Initialize(int threadId)
    {
        OwnerThreadId = threadId;
    }

    internal void Reset()
    {
        OwnerThreadId = -1;
        if (device.DebugMode) {
            AllocationStackTrace = null;
        }
        // TODO rest internal resources / offsets
    }
    
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)] [StackTraceHidden]
    protected void ValidateThreadSafety()
    {
        if (OwnerThreadId != Environment.CurrentManagedThreadId) {
            ThrowInvalidThread();
        }
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)] [StackTraceHidden] [DoesNotReturn]
    internal void ThrowInvalidThread()
    {
        var name = Thread.CurrentThread.Name ?? "unknown thread";
        throw new InvalidOperationException(
                $"[Thread Context Violation] method executes on thread: {Environment.CurrentManagedThreadId} ({name})" +
                $"but PipelineContext belongs to thread {OwnerThreadId}!");
    }
}
