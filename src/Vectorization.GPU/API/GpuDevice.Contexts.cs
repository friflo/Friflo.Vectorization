// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

// ReSharper disable UseNullPropagation
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

public abstract partial class GpuDevice
{
    private  readonly   ContextPool                     pool            = new ();
    internal readonly   ThreadLocal<PipelineContext>    threadContexts  = new (trackAllValues: true);

    
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
        newRecorder.Initialize(currentThreadId, file, line);

        threadContexts.Value = newRecorder;

        return newRecorder;
    }
    
    /// <summary> Is only called by <see cref="PipelineContext.Dispose"/> </summary>
    internal void EndContext(PipelineContext context)
    {
        threadContexts.Value = null;    // clear thread storage first

        pool.Return(context);
    }
    
    public virtual void Dispose()
    {
        if (IsDisposed) {
             return;
        }
        
        List<string> leaks = null;

        // Scan the ThreadLocal storage for any forgotten/unclosed contexts
        foreach (var context in threadContexts.Values) 
        {
            // If a context is still attached to a thread and IsDisposed is false, the developer forgot to close it via the 'using' block.
            if (context != null && !context.IsDisposed) {
                leaks ??= new List<string>();
                leaks.Add($"  -> Left Context open on Thread: {context.ownerThreadId} ! Opened at: {context.callerFile}:{context.callerLine}");
                
                // CRITICAL: We intentionally DO NOT call context.Dispose() here.
                // If the developer leaked resources, we can not silently fix it here. native wgpu resource cannot be released from different thread.
                // We let it crash so it gets fixed in the user code immediately.
            }
        }

        // Hard crash if resource leaks were detected
        if (leaks != null)
        {
            var errorLog = "[Resource Leak Detected] GpuDevice.Dispose() failed because active PipelineContexts were not closed!\n" + 
                              string.Join("\n", leaks) + "\nFix this by wrapping your contexts in a 'using' block.";
            throw new InvalidOperationException(errorLog);
        }
        threadContexts.Dispose();
        pool.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// ConcurrentQueue is lock-free. Spin-Wait's on failed operations, but does not lock
internal class ContextPool : IDisposable
{
    // used ConcurrentQueue<T> in favor of ConcurrentStack<T>
    private readonly ConcurrentQueue<PipelineContext> pooled = [];
    
    public  override    string  ToString() => $"pooled: {pooled.Count}";


    internal PipelineContext Fetch(GpuDevice device)
    {
        if (pooled.TryDequeue(out var context)) {
            return context;
        }
        return device.NewPipelineContext();
    }

    internal void Return(PipelineContext context)
    {
        pooled.Enqueue(context);
        // ConcurrentStack<CommandList>.Push() allocates Node -> 40 bytes 
    }

    public void Dispose()
    {
        pooled.Clear();
        
        // alternative approach, if unmanaged resource need to be reused
        // while (pooled.TryPop(out var context)) {
        //     // context.ReleaseUnmanaged(); 
        // }
    }
}

