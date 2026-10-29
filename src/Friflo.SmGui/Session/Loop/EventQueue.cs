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
/// Intentionally hidden from direct user code - accessible only via <see cref="SmClient.EventQueue"/>.<br/>
/// Enqueuing additional or invalid events would break the state / invariant.  
/// </remarks>
public sealed class SmEventQueue
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
    
    /// <summary>
    /// Tries to enqueue an event without blocking.<br/>
    /// If the queue is saturated, the event is dropped and its payload is returned to the pool.<br/>
    /// The method is thread-safe and can be called from any thread.
    /// </summary>
    /// <returns>True if the event was queued; false if it was dropped due to backpressure.</returns>
    public bool TryEnqueueEvent(LoopEventType type, object? state, Payload payload, int data = 0)
    {
        return TryEnqueueEventSync(null, default, type, state, payload, data);
    }

    /// <summary>
    /// Tries to enqueue an event for a specific client without blocking.<br/>
    /// If the queue is saturated, the event is dropped and its payload is returned to the pool.<br/>
    /// The method is thread-safe and can be called from any thread.
    /// </summary>
    /// <returns>True if the event was queued; false if it was dropped due to backpressure.</returns>
    public bool TryEnqueueClientEvent(SmClient client, LoopEventType type, Payload payload)
    {
        return TryEnqueueEventSync(client, client.sessionId, type, null, payload, 0);
    }
    
    internal bool TryEnqueueEventSync(SmClient? client, SessionId sessionId, LoopEventType type, object? state, Payload payload, int data)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        var evt = new LoopEvent(sessionId, type, state, payload, data);

        // Synchronous path backpressure check
        if (queue.Count >= MaxSyncQueueCapacity) {
            // Queue capacity reached: drop event and release memory back to pool
            payload.Return();
            return false;
        }
        client?.IncrementPendingEvents();
        queue.Enqueue(evt);
        eventReady.Set(); // Wake up worker thread
        return true;
    }
}