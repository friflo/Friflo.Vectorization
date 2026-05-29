// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Friflo.Vectorization.GPU.Runtime;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;


public enum PipelineTraceType : byte
{
    Kernel,
    Kernel_Submit,
    Batch_Submit,
    Pass_Split_RAW,
    Pass_Split_WAR,
    Pass_Split_WAW,
}

public struct PipelineStats
{
    /// <summary>Total number of dispatched GPU kernels; higher means more workload processed.</summary>
    public int Calls;

    /// <summary>Total hardware passes generated; target 1 to ensure everything runs in a single batch.</summary>
    public int Passes;

    /// <summary>Total pipeline stalls detected; hunt this down to 0 for maximum performance.</summary>
    public int Hazards;

    public override string ToString() => $"calls: {Calls}  passes: {Passes}  hazards: {Hazards}";
}

public struct PipelineTrace
{
    public  PipelineTraceType   TraceType;
    public  string              KernelName => KernelRegistry.GetKernelName(KernelId);
    public  int                 KernelId;
    public  int                 Calls;
    public  int                 Passes;
    public  string              Resource;

    public override string      ToString() => Append(new StringBuilder()).ToString();
    
    internal StringBuilder Append(StringBuilder sb)
    {
        switch (TraceType) {
            case PipelineTraceType.Kernel:
                sb.Append($"{KernelName}()  calls: {Calls}  passes: {Passes}");
                break;
            case PipelineTraceType.Kernel_Submit:
                sb.Append($"[Kernel_Submit]  {KernelName}()");
                break;
            case PipelineTraceType.Batch_Submit:
                sb.Append($"[Batch_Submit]");
                break;
            case PipelineTraceType.Pass_Split_RAW:
                sb.Append($"[Pass_Split - RAW]  Resource: '{Resource}'");
                break;
            case PipelineTraceType.Pass_Split_WAR:
                sb.Append($"[Pass_Split - WAR]  Resource: '{Resource}'");
                break;
            case PipelineTraceType.Pass_Split_WAW:
                sb.Append($"[Pass_Split - WAW]  Resource: '{Resource}'");
                break;
        }
        return sb;
    } 
}

public struct KernelMetric
{
    public  string  KernelName => KernelRegistry.GetKernelName(KernelId);
    public  int     KernelId;
    public  int     Calls;
    public  int     Passes;
    
    public override  string ToString() => $"{KernelName}()  calls: {Calls}  passes: {Passes}";
}

public readonly ref struct PipelineContext
{
    public  bool                        EnablePassBatching  { get => recorder.EnablePassBatching; set => recorder.EnablePassBatching = value; } 
    public  bool                        EnableTraces        { get => recorder.EnableTraces;       set => recorder.EnableTraces = value; }
    public  PipelineStats               Stats               => recorder.GetStats();
    public  ReadOnlySpan<PipelineTrace> Traces              => recorder.GetTraces();
    public  ReadOnlySpan<KernelMetric>  KernelMetrics       => recorder.GetKernelMetrics();
    public  string                      TraceLog            => AppendTraceLog (new StringBuilder()).ToString();
    public  string                      KernelMetricLog     => AppendMetricLog(new StringBuilder()).ToString();
    public  void                        ClearTraces()       => recorder.ClearTraces();
    public  void                        ClearKernelMetrics()=> recorder.ClearKernelMetrics();
    
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly PipelineRecorder recorder; 

    public  override string             ToString()          => AppendToString(new StringBuilder()).ToString();
    
    public PipelineContext(PipelineRecorder recorder) {
        this.recorder = recorder;
    }
    
    private StringBuilder AppendToString(StringBuilder sb)
    {
        sb.Append($"Batching: {EnablePassBatching}  Traces: {EnableTraces}  Count: {Traces.Length}");
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
        foreach (var trace in Traces) {
            sb.Append('\n');
            trace.Append(sb);
        }
        return sb;
    }
    
    private StringBuilder AppendMetricLog(StringBuilder sb)
    {
        sb.Append($"--- KERNEL METRIC ---");
        foreach (var metric in KernelMetrics) {
            if (metric.Calls == 0) {
                continue;   
            }
            sb.Append($"\n{metric.KernelName}()  calls: {metric.Calls}  passes: {metric.Passes}");
        }
        return sb;
    }
}

public static class KernelRegistry
{
    private static          string[]    kernelNames = new string[20];
    private static readonly object      mutex = new();
    private static          int         nextId;
    
    internal static         string      GetKernelName(int slot) => kernelNames[slot];
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int NewKernelId(string kernelName)
    {
        lock (mutex)
        {
            var newId = ++nextId;
            if (newId >= kernelNames.Length) {
                var newNames = new string[2 * kernelNames.Length];
                Array.Copy(kernelNames, newNames, kernelNames.Length);
                newNames[newId] = kernelName;
                kernelNames = newNames;
            } else {
                kernelNames[newId] = kernelName;
            }
            return newId;
        }
    }
}

