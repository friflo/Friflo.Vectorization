// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Friflo.SmGui.Session;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui;


/// <summary>
/// Provides an awaiter that transitions the execution context to the single-threaded session loop,
/// enforcing thread confinement without heap allocations.
/// </summary>
/// <remarks>
/// Use <see cref="ThreadPoolAwaiter"/> to switch execution to worker thread.
/// </remarks>
public readonly struct LoopAwaiter : ICriticalNotifyCompletion
{
    private readonly SmEventQueue eventQueue;

    internal LoopAwaiter(SmSessionLoop loop) => eventQueue = loop.eventQueue;
    
    public bool         IsCompleted     => false; // loop.IsCurrentThread; as possible alternative
    public LoopAwaiter  GetAwaiter()    => this;
    public void         GetResult()     { }

    public void OnCompleted(Action continuation) {
        eventQueue.TryEnqueueEvent(null, default, LoopEventType.DispatchAsync, continuation, default, 0);
    }

    public void UnsafeOnCompleted(Action continuation) {
        eventQueue.TryEnqueueEvent(null, default, LoopEventType.DispatchAsync, continuation, default, 0);
    }
}


/// <summary>
/// Awaiter that forces continuation onto a <see cref="ThreadPool"/> worker thread.
/// </summary>
/// <remarks>
/// Use <see cref="LoopAwaiter"/> to dispatch execution on loop (main) thread.
/// </remarks>
public readonly struct ThreadPoolAwaiter : INotifyCompletion
{
    /// <summary>
    /// Returns an awaiter to asynchronously switch execution to a <see cref="ThreadPool"/> worker thread.
    /// </summary>
    public static ThreadPoolAwaiter SwitchToAsync() => default;

    public bool IsCompleted => false; // always force async continuation so it cannot run inline on the current thread
    
    public void OnCompleted(Action continuation) {
        ThreadPool.UnsafeQueueUserWorkItem(static state => ((Action)state!)(), continuation);
    }

    public void GetResult() { }

    public ThreadPoolAwaiter GetAwaiter() => this;
}

