// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Friflo.Vectorization.GPU.Runtime;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU;


public sealed partial class WgpuDevice
{
    private readonly    RecorderPool                    pool            = new ();
    private readonly    ThreadLocal<CommandRecorder>    threadRecorders = new (trackAllValues: false);
    public  override    PipelineContext                 Context         => threadRecorders.Value;
    
    public CommandRecorder Recorder
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            var recorder = threadRecorders.Value;
            if (recorder == null) throw MissingContextException();
            recorder.ValidateThreadSafety();
            return recorder;
        }
    }
    
    private InvalidOperationException MissingContextException()
    {
        return new InvalidOperationException($"Missing Context on device: '{Label}'. Call device.BeginContext() first.");
    }

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


internal class RecorderPool : IDisposable
{
    private readonly    ConcurrentBag<CommandRecorder>  storage     = [];
    private readonly    List<CommandRecorder>           allCreated  = [];

    internal CommandRecorder Fetch(WgpuDevice device)
    {
        if (storage.TryTake(out var recorder)) {
            return recorder;
        }
        var newRecorder = new CommandRecorder(device);
        lock (allCreated) {
            allCreated.Add(newRecorder);
        }
        return newRecorder; 
    }

    internal void Return(CommandRecorder recorder)
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