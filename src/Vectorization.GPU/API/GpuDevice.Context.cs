// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Friflo.Vectorization.GPU.Runtime;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

public abstract partial class GpuDevice
{
    private readonly    ContextPool                     pool            = new ();
    private readonly    ThreadLocal<PipelineContext>    threadContexts  = new (trackAllValues: false);

    
    protected internal abstract PipelineContext NewPipelineContext();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)] [StackTraceHidden]
    protected static void ValidateThreadSafety(PipelineContext context)
    {
        if (context.OwnerThreadId != Environment.CurrentManagedThreadId) {
            context.ThrowInvalidThread();
        }
    }
    
    [StackTraceHidden]
    private PipelineContext BeginContextInternal()
    {
        var existingContext = threadContexts.Value;
        if (existingContext != null)
        {
            if (DebugMode) {
                string stackTrace = existingContext.AllocationStackTrace ?? "Unknown allocation point";
                throw new InvalidOperationException(
                    $"[Engine Error] PipelineContext-Leak detected! EndContext() was not called on this thread.\n" +
                    $"PipelineContext was opened at:\n{stackTrace}");
            }
            existingContext.Reset();
            pool.Return(existingContext);
            threadContexts.Value = null;
        }

        var newRecorder = pool.Fetch(this);

        int currentThreadId = Environment.CurrentManagedThreadId;
        newRecorder.Initialize(currentThreadId);

        if (DebugMode) {
            newRecorder.AllocationStackTrace = Environment.StackTrace;
        }
        threadContexts.Value = newRecorder;

        return newRecorder;
    }
}


internal class ContextPool : IDisposable
{
    private readonly    ConcurrentBag<PipelineContext>  storage     = [];
    private readonly    List<PipelineContext>           allCreated  = [];

    internal PipelineContext Fetch(GpuDevice device)
    {
        if (storage.TryTake(out var context)) {
            return context;
        }
        var newContext = device.NewPipelineContext();
        lock (allCreated) {
            allCreated.Add(newContext);
        }
        return newContext; 
    }

    internal void Return(PipelineContext context)
    {
        storage.Add(context);
    }

    public void Dispose()
    {
        lock (allCreated) {
            foreach (var context in allCreated) {
                context.Dispose(); 
            }
            allCreated.Clear();
        }
        while (storage.TryTake(out _));
    }
}

