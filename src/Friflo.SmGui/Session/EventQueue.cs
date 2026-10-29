// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Threading;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Session;


public class SmEventQueue
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
    
    /*
    // --- enqueue async
    public ValueTask EnqueueEventAsync(LoopEventType type, object? state, Payload payload, int data = 0)
    {
        return EnqueueAsync(default, type, state, payload, data);
    }
    
    public ValueTask EnqueueClientEventAsync(SmClient client, LoopEventType type, Payload payload)
    {
        client.IncrementPendingEvents();
        return EnqueueAsync(client.sessionId, type, null, payload, 0);
    }
    
    private async ValueTask EnqueueAsync(SessionId sessionId, LoopEventType type, object? state, Payload payload, int data)
    {
        var evt = new LoopEvent(sessionId, type, state, payload, data);

        if (isAsync) {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            await eventChannel!.Writer.WriteAsync(evt);
            return;
        }
        eventQueue.Enqueue(evt);
        eventReady.Set(); // wake up Thread
    } */
    
    
    
    // --- enqueue sync
    
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

        /* if (isAsync)
        {
            client?.IncrementPendingEvents();
            // Non-blocking try-write to the channel
            if (eventChannel!.Writer.TryWrite(evt)) {
                return true;
            }
            client?.DecrementPendingEvents(); // decrement - TryWrite() failed
            // Backpressure triggered: drop event and release memory back to pool
            payload.Return();
            return false;
        } */

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