// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Session;


/// <summary>
/// The event queue owned by the <see cref="SmSessionLoop"/>.
/// </summary>
/// <remarks>
/// Intentionally hidden from direct user code - accessible only via <see cref="SmClient.eventQueue"/>.<br/>
/// Enqueuing additional or invalid events would break the state / invariant.  
/// </remarks>
internal sealed class SmEventQueue
{
    private  readonly   ConcurrentQueue<LoopEvent>  queue;
    private  readonly   AutoResetEvent              eventReady;
    internal            SmEventReady?               wasmEventReady;
    internal            SmRuntime                   runtime = new();
    internal            bool                        isDisposed;
    
    private  const      int                         MaxQueueCapacity = 16384; // 16384 events == 512 KB memory

    internal SmEventQueue(SmSessionLoop loop)
    {
        queue           = loop.queue;
        eventReady      = loop.eventReady;
    }
    
    internal bool TryEnqueueEvent(SmClient? client, SessionId sessionId, LoopEventType type, object? state, Payload payload, int data)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        var queueCount = queue.Count;
        
        // Synchronous path backpressure check
        if (queueCount >= MaxQueueCapacity) {
            // Queue capacity reached: drop event and release memory back to pool
            payload.Return();
            return false;
        }
        client?.IncrementPendingEvents();
        
        var ev = new LoopEvent(sessionId, type, state, payload, data);

        queue.Enqueue(ev);
        eventReady.Set(); // Wake up worker thread

        wasmEventReady?.Set(); // Wake up main thread to run:  ProcessEvents() > IterateSessions()
        return true;
    }
}

internal sealed class AsyncSignal
{
    private readonly SemaphoreSlim semaphore = new(0, 1);

    public void Set()
    {
        if (semaphore.CurrentCount == 0) {
            semaphore.Release();
        }
    }

    public async Task WaitAsync()
    {
        await semaphore.WaitAsync();
    }
}