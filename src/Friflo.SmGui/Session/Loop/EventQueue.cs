// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Threading;

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
    internal            bool                        isDisposed;
    
    private  const      int                         MaxSyncQueueCapacity = 16384; // 16384 events == 512 KB memory

    internal SmEventQueue(ConcurrentQueue<LoopEvent>  queue, AutoResetEvent eventReady)
    {
        this.queue      = queue;
        this.eventReady = eventReady;
    }
    
    internal bool TryEnqueueEvent(SmClient? client, SessionId sessionId, LoopEventType type, object? state, Payload payload, int data)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        // Synchronous path backpressure check
        if (queue.Count >= MaxSyncQueueCapacity) {
            // Queue capacity reached: drop event and release memory back to pool
            payload.Return();
            return false;
        }
        client?.IncrementPendingEvents();
        
        queue.Enqueue(new LoopEvent(sessionId, type, state, payload, data));
        eventReady.Set(); // Wake up worker thread
        return true;
    }
}