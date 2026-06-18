// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable InconsistentNaming
// ReSharper disable InvertIf
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU.Runtime;

/// --- <see cref="PipelineContext"/> ---
public sealed partial class CommandRecorder
{
    private     PipelineStats   pipelineStats;
    internal    bool            enableTraces;
    private     PipelineTrace[] traces;
    private     int             traceCount;
    private     bool            traceNewKernel;
    private     KernelMetric[]  kernelMetrics       = [default];
    private     int             kernelMetricCount;
    
    public override  PassBatching PassBatching
    {
        get {
            ValidateThreadSafety();
            return enablePassBatching;
        }
        set {
            ValidateThreadSafety();
            enablePassBatching = value;
        }
    }

    public override  bool EnableTraces
    {
        get {
            ValidateThreadSafety();
            return enableTraces;
        }
        set {
            ValidateThreadSafety();
            if (value) {
                traces ??= new PipelineTrace[10];
            }
            traceCount          = 0;
            pipelineStats       = default;
            enableTraces        = value;
        }
    }

    public override void ClearTraces()
    {
        ValidateThreadSafety();
        traceCount     = 0;
        pipelineStats  = default;
    }
    
    public override void ClearKernelMetrics()
    {
        ValidateThreadSafety();
        var metrics = kernelMetrics;
        var count   = kernelMetricCount;
        for (int n = 1; n <= count; n++) {
            ref var metric = ref metrics[n];
            metric.Calls    = 0;
            metric.Passes   = 0;
        }
    }

    protected override  PipelineStats GetStats() {
        ValidateThreadSafety();
        return pipelineStats;
    }

    protected override  ReadOnlySpan<PipelineTrace> GetTraces() {
        ValidateThreadSafety();
        return traces.AsSpan(0, traceCount);
    }

    protected override  ReadOnlySpan<KernelMetric>  GetKernelMetrics() {
        ValidateThreadSafety();
        return kernelMetrics.AsSpan(1, kernelMetricCount);
    }
    
    protected override QueueStats GetQueueStats() {
        ValidateThreadSafety();
        var list = commandList;
        return new QueueStats {
            Commands    = list.commands.Count,
            Ranges      = list.idRanges.Count
        };
    }
    
    protected override void Initialize(int threadId, string file, int line)
    {
        base.Initialize(threadId, file, line);

        uniformBuffer ??= (WgpuBuffer<byte>)device.CreateBuffer<byte>(uniformBufferSize, 0, device.Label, BufferProfile.StaticIn, BufferType.Uniform);
    }
    
    protected override unsafe void ReleaseResources()
    {
        uniformBuffer?.Dispose();
        uniformBuffer = null;
        for (int n = 0; n < computeUniformGroups.Length; n++) {
            ref var group = ref computeUniformGroups[n];
            if (group.handle != null) {
                wgpuBindGroupRelease(group.handle);
                group = default;
            }
        }
        for (int n = 0; n < shaderUniformGroups.Length; n++) {
            ref var group = ref shaderUniformGroups[n];
            if (group.handle != null) {
                wgpuBindGroupRelease(group.handle);
                group = default;
            }
        }
    }
    
    /// --- <see cref="PipelineTrace"/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void AddTrace(TraceType traceType, int kernel = 0, int calls = 0, string resource = null, TraceSubType subType = TraceSubType.None)
    {
        var localTraces = traces;
        if (traceCount >= localTraces.Length) {
            localTraces = ResizeTraces();
        }
        ref var trace = ref localTraces[traceCount++];
        trace.TraceType = traceType;
        trace.ShaderId  = kernel;
        trace.Calls     = calls;
        trace.SubType   = subType;
        trace.Resource  = resource;
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal unsafe void AddKernelTrace(int kernel)
    {
        var localTraces = traces;
        if (traceCount >= localTraces.Length) {
            localTraces = ResizeTraces();
        }
        ref var trace = ref localTraces[traceCount++];
        trace.TraceType = currentPass != null ? TraceType.Kernel : TraceType.Shader;
        trace.ShaderId  = kernel;
        trace.Calls     = 1;
        trace.SubType   = createNewPass ? (kernelSeq == 1 ? TraceSubType.NewPass : TraceSubType.PassSplit) : TraceSubType.None;
        trace.Resource  = null;
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void UpdateKernelTrace()
    {
        if (traceNewKernel) {
            AddKernelTrace(kernelId);
        } else {
            traces[traceCount - 1].Calls++;
        }
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
