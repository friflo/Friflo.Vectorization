// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization.GPU;

// ReSharper disable InconsistentNaming
// ReSharper disable InvertIf
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;

public sealed partial class CommandRecorder
{
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
} 