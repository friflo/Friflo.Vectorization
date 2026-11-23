// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Friflo.SmGui.Runtime;

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
    /// <see cref="eventReady"/> is only used / relevant when using <see cref="SmSessionLoop.Run"/>
    internal            AutoResetEvent?             eventReady; 
    internal            SmRuntime                   runtime = new();
    private             bool                        isDisposed;
    internal            SmRuntimeEventHandler?      runtimeEventHandler;

    
    private  const      int                         MaxQueueCapacity = 16384; // 16384 events == 512 KB memory

    internal SmEventQueue(SmSessionLoop loop)
    {
        queue = loop.queue;
    }
    
    internal bool TryEnqueueEvent(SmClient? client, SessionId sessionId, LoopEventType type, object? state, Payload payload, int data)
    {
        if (isDisposed) {
            return false;
        }

        var queueCount = queue.Count;
        
        // Synchronous path backpressure check
        if (queueCount >= MaxQueueCapacity) {
            // Queue capacity reached: drop event and release memory back to pool
            payload.Return();
            return false;
        }
        client?.IncrementPendingEvents();
        
        queue.Enqueue(new LoopEvent(sessionId, type, state, payload, data));
        eventReady?.Set(); // Wake up worker thread

        runtimeEventHandler?.NotifyEventReady();

        return true;
    }
    
    /// Clears queue and adds a single event to exit the loop
    internal void ExitLoop()
    {
        isDisposed = true; // avoid enqueuing any other event 
        queue.Clear();
        
        queue.Enqueue(new LoopEvent(default, LoopEventType.ExitLoop, null, default, 0));
        eventReady?.Set(); // Wake up worker thread
        
        runtimeEventHandler?.NotifyEventReady();
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