// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Text;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;


public enum PipelineTraceType : byte
{
    Kernel,
    KernelSubmit,
    BatchSubmit,
    PassSplitRAW,
    PassSplitWAR
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
                sb.Append($"'{KernelName}'  calls: {Calls}  passes: {Passes}");
                break;
            case PipelineTraceType.KernelSubmit:
                sb.Append($"[KernelSubmit]  '{KernelName}'");
                break;
            case PipelineTraceType.BatchSubmit:
                sb.Append($"[BatchSubmit]");
                break;
            case PipelineTraceType.PassSplitRAW:
                sb.Append($"[Pass Split - RAW]  Resource: '{Resource}'");
                break;
            case PipelineTraceType.PassSplitWAR:
                sb.Append($"[Pass Split - WAR]  Resource: '{Resource}'");
                break;
        }
        return sb;
    } 
}

public class PipelineContext
{
    public    virtual   bool                            EnablePassBatching  { get; set; }
    public    virtual   bool                            EnableTraces        { get; set; }
    public              PipelineStats                   Stats           => GetStats();
    public              ReadOnlySpan<PipelineTrace>     Traces          => GetTraces();
    public              string                          TraceLog        => AppendTraceLog(new StringBuilder()).ToString();
    public    virtual   void                            ClearTraces()   { }
    
    protected virtual   PipelineStats                   GetStats()      => default;
    protected virtual   ReadOnlySpan<PipelineTrace>     GetTraces()     => default;

    public    override  string ToString() => $"Batching: {EnablePassBatching}  Traces: {EnableTraces}  Traces: {Traces.Length}";
    

    private StringBuilder AppendTraceLog(StringBuilder sb)
    {
        sb.Append($"--- PIPELINE TRACE ({this}) ---");
        if (EnablePassBatching) {
            sb.Append("\n// Lock-free GPU kernels with deferred, hazard-driven pass batching");
        }
        foreach (var trace in Traces) {
            sb.Append('\n');
            trace.Append(sb);
        }
        return sb;
    }
}

public static class KernelRegistry
{
    private static          string[]    kernelNames = new string[20];
    private static readonly object      mutex = new();
    private static          int         nextId;
    
    internal static string GetKernelName(int slot) => kernelNames[slot];
    
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

