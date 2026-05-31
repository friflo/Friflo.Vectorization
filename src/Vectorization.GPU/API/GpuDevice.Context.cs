// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

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
        if (context.ownerThreadId != Environment.CurrentManagedThreadId) {
            context.ThrowInvalidThread();
        }
    }
    
    [StackTraceHidden]
    private PipelineContext BeginContextInternal(string file, int line)
    {
        var existingContext = threadContexts.Value;
        if (existingContext != null) {
            throw new InvalidOperationException(
                $"[Context Conflict] A PipelineContext is already active on this thread. Was opened at: {existingContext.callerFile}:{existingContext.callerLine}");
        }
        var newRecorder = pool.Fetch(this);

        int currentThreadId = Environment.CurrentManagedThreadId;
        newRecorder.Initialize(currentThreadId);

        newRecorder.callerFile = file;
        newRecorder.callerLine = line;
        threadContexts.Value = newRecorder;

        return newRecorder;
    }
}

/// ConcurrentStack is lock-free. Spin-Wait's on failed operations, but does not lock
internal class ContextPool : IDisposable
{
    private readonly ConcurrentStack<PipelineContext> storage       = [];
    private readonly ConcurrentStack<PipelineContext> allCreated    = [];

    internal PipelineContext Fetch(GpuDevice device)
    {
        if (storage.TryPop(out var context)) {
            return context;
        }
        
        var newContext = device.NewPipelineContext();
        allCreated.Push(newContext); // CAS-Operation (Compare-And-Swap), extreme fast
        return newContext; 
    }

    internal void Return(PipelineContext context)
    {
        storage.Push(context);
    }

    public void Dispose()
    {
        while (allCreated.TryPop(out var context)) {
            context.Dispose();
        }
        allCreated.Clear();
        storage.Clear();
    }
}

