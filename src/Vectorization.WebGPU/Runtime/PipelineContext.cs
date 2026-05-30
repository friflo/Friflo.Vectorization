// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;

// ReSharper disable InconsistentNaming
// ReSharper disable InvertIf
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;

/// --- <see cref="PipelineContext"/> ---
public sealed partial class CommandRecorder
{
    private             PipelineStats       pipelineStats;
    private             bool                enableTraces;
    private             PipelineTrace[]     traces;
    private             int                 traceCount;
    private             bool                traceNewKernel;
    private             KernelMetric[]      kernelMetrics       = [default];
    private             int                 kernelMetricCount;
    
    protected override  bool EnablePassBatching { get => enablePassBatching; set => enablePassBatching = value; }
    protected override  bool EnableTraces
    {
        get => enableTraces;
        set {
            traces       ??= new PipelineTrace[10];
            traceCount     = 0;
            pipelineStats  = default;
            enableTraces   = value;
        }
    }
    
    protected override void ClearTraces()
    {
        traceCount     = 0;
        pipelineStats  = default;
    }
    
    protected override void ClearKernelMetrics()
    {
        var metrics = kernelMetrics;
        var count   = kernelMetricCount;
        for (int n = 1; n <= count; n++) {
            ref var metric = ref metrics[n];
            metric.Calls    = 0;
            metric.Passes   = 0;
        }
    }

    protected override  PipelineStats               GetStats()          => pipelineStats;
    protected override  ReadOnlySpan<PipelineTrace> GetTraces()         => traces.AsSpan(0, traceCount);
    protected override  ReadOnlySpan<KernelMetric>  GetKernelMetrics()  => kernelMetrics.AsSpan(1, kernelMetricCount);
    
    
    /// --- <see cref="PipelineTrace"/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddTrace(TraceType traceType, int kernel = 0, string resource = null)
    {
        var localTraces = traces;
        if (traceCount >= localTraces.Length) {
            localTraces = ResizeTraces();
        }
        ref var trace = ref localTraces[traceCount++];
        trace.TraceType = traceType;
        trace.KernelId  = kernel;
        trace.Calls     = 0;
        trace.SubType   = TraceSubType.None;
        trace.Resource  = resource;
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddKernelTrace(TraceType traceType, int kernel)
    {
        var localTraces = traces;
        if (traceCount >= localTraces.Length) {
            localTraces = ResizeTraces();
        }
        ref var trace = ref localTraces[traceCount++];
        trace.TraceType = traceType;
        trace.KernelId  = kernel;
        trace.Calls     = 1;
        trace.SubType   = createNewPass ? (kernelSeq == 1 ? TraceSubType.NewPass : TraceSubType.PassSplit) : TraceSubType.None;
        trace.Resource  = null;
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private PipelineTrace[] ResizeTraces()
    {
        var localTraces = traces;
        var newTraces  = new PipelineTrace[localTraces.Length * 2];
        Array.Copy(localTraces, 0, newTraces, 0, localTraces.Length);
        return traces = newTraces;
    }
    
    /// --- <see cref="KernelMetric"/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ResizeAndIncrementMetric(int kernel)
    {
        var metrics     = kernelMetrics;
        var newMetrics  = new KernelMetric[Math.Max(metrics.Length * 2, kernel + 1)];
        Array.Copy(metrics, 0, newMetrics, 0, metrics.Length);
        for (int id = metrics.Length; id < newMetrics.Length; id++) {
            newMetrics[id].KernelId = id;
        }
        newMetrics[kernel].Calls++;
        kernelMetrics       = newMetrics;
        kernelMetricCount   = Math.Max(kernelMetricCount, kernel);
    }
} 