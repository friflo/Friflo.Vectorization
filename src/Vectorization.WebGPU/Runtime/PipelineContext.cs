// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Friflo.Vectorization.GPU;

// ReSharper disable InconsistentNaming
// ReSharper disable InvertIf
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;

public class WgpuPipelineContext : PipelineContext
{
    public    override  bool EnablePassBatching { get => recorder.enablePassBatching; set => recorder.enablePassBatching = value; }
    public    override  bool EnableTraces
    {
        get => recorder.enableTraces;
        set {
            recorder.traces       ??= new PipelineTrace[10];
            recorder.traceCount     = 0;
            recorder.pipelineStats  = default;
            recorder.enableTraces   = value;
        }
    }
    
    public override void ClearTraces()
    {
        recorder.traceCount     = 0;
        recorder.pipelineStats  = default;
    }
    
    public override void ClearKernelMetrics()
    {
        var metrics = recorder.kernelMetrics;
        var count   = recorder.kernelMetricCount;
        for (int n = 1; n <= count; n++) {
            ref var metric = ref metrics[n];
            metric.Calls    = 0;
            metric.Passes   = 0;
        }
    }

    protected override  PipelineStats               GetStats()          => recorder.pipelineStats;
    protected override  ReadOnlySpan<PipelineTrace> GetTraces()         => recorder.traces.AsSpan(0, recorder.traceCount);
    protected override  ReadOnlySpan<KernelMetric>  GetKernelMetrics()  => recorder.kernelMetrics.AsSpan(1, recorder.kernelMetricCount);
    
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private  readonly   CommandRecorder recorder;
    
    internal WgpuPipelineContext(CommandRecorder recorder) {
        this.recorder = recorder;
    }
} 