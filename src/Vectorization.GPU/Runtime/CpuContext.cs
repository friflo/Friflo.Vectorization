// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Friflo.Vectorization.GPU.Runtime;


// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.CPU;


internal sealed class CpuContext : PipelineContext, IDisposable
{
    private  readonly   CpuDevice   device;
    internal            int         OwnerThreadId { get; private set; }
    internal            string      AllocationStackTrace { get; set; }

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
    }
    
    internal CpuContext(CpuDevice device) {
        this.device = device;
    }

    public void Dispose() { }
}



internal sealed partial class CpuDevice
{
    private readonly    CpuContextPool             pool        = new ();
    private readonly    ThreadLocal<CpuContext>    threadRecorders = new (trackAllValues: false);
    public  override    PipelineContext            Context     => threadRecorders.Value;

    public override PipelineContext BeginContext()
    {
        var existingRecorder = threadRecorders.Value;
        if (existingRecorder != null)
        {
            if (DebugMode) {
                string stackTrace = existingRecorder.AllocationStackTrace ?? "Unknown allocation point";
                throw new InvalidOperationException(
                    $"[Engine Error] PipelineContext-Leak detected! EndContext() was not called on this thread.\n" +
                    $"PipelineContext was opened at:\n{stackTrace}");
            }
            existingRecorder.Reset();
            pool.Return(existingRecorder);
            threadRecorders.Value = null;
        }

        var newRecorder = pool.Fetch(this);

        int currentThreadId = Environment.CurrentManagedThreadId;
        newRecorder.Initialize(currentThreadId);

        if (DebugMode) {
            newRecorder.AllocationStackTrace = Environment.StackTrace;
        }
        threadRecorders.Value = newRecorder;

        return newRecorder;
    }
}


internal class CpuContextPool : IDisposable
{
    private readonly    ConcurrentBag<CpuContext>  storage     = [];
    private readonly    List<CpuContext>           allCreated  = [];

    internal CpuContext Fetch(CpuDevice device)
    {
        if (storage.TryTake(out var recorder)) {
            return recorder;
        }
        var newRecorder = new CpuContext(device);
        lock (allCreated) {
            allCreated.Add(newRecorder);
        }
        return newRecorder; 
    }

    internal void Return(CpuContext recorder)
    {
        storage.Add(recorder);
    }

    public void Dispose()
    {
        lock (allCreated) {
            foreach (var recorder in allCreated) {
                recorder.Dispose(); 
            }
            allCreated.Clear();
        }
        while (storage.TryTake(out _));
    }
}


