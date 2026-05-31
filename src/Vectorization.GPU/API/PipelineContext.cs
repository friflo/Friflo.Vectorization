// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public class PipelineContext : IDisposable
{
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
}
