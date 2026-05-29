// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public class PipelineRecorder
{
    protected internal virtual  bool                        EnablePassBatching  { get; set; }
    protected internal virtual  bool                        EnableTraces        { get; set; }
    protected internal virtual  void                        ClearTraces()       { }
    protected internal virtual  void                        ClearKernelMetrics(){ }
    
    protected internal virtual  PipelineStats               GetStats()          => default;
    protected internal virtual  ReadOnlySpan<PipelineTrace> GetTraces()         => default;
    protected internal virtual  ReadOnlySpan<KernelMetric>  GetKernelMetrics()  => default;
}
