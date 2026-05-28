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
    public    override  bool EnableDiagnostics
    {
        get => recorder.enableDiagnostics;
        set {
            recorder.records ??= new PipelineRecord[10];
            recorder.recordCount = 0;
            recorder.enableDiagnostics = value;
        }
    }
    
    public    override  void                            ClearRecords()  {  recorder.recordCount = 0; }

    protected override  ReadOnlySpan<PipelineRecord>    GetRecords()    => recorder.records.AsSpan(0, recorder.recordCount);
    
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private  readonly   CommandRecorder recorder;
    
    internal WgpuPipelineContext(CommandRecorder recorder) {
        this.recorder = recorder;
    }
} 