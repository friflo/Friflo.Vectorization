// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Hide = System.Diagnostics.DebuggerHiddenAttribute;

// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable UnusedMember.Global
// ReSharper disable ArrangeThisQualifier
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Session;


public readonly struct SmSendCommand
{
    public readonly string  payload;
    public override string  ToString()      => payload;

    internal SmSendCommand(string payload)  => this.payload = payload;
}


public enum SmSendMessageMode {
    Queue       = 0,
    DropUnsent  = 1
}

public readonly struct SmSendMessage
{
    public readonly ReadOnlyMemory<byte>    buffer;
    public readonly SmSendMessageMode       mode;
    
    public          int                     Length  => buffer.Length;
    public          bool                    IsEmpty => buffer.IsEmpty;

    public override string                  ToString() => $"{mode} [{Length}]";

    [Hide]
    public SmSendMessage(ReadOnlyMemory<byte> buffer) {
        this.buffer = buffer;
        this.mode   = SmSendMessageMode.Queue;
    }
    
    [Hide]
    public SmSendMessage(ReadOnlyMemory<byte> buffer, SmSendMessageMode mode) {
        this.buffer = buffer;
        this.mode   = mode;
    }
}


internal struct SendQueueState
{
    internal    long    lastAppendedTime;
    internal    long    lastSentTime;
    internal    int     queueSize;
}


public abstract class SmClient
{
    private             SendQueueState  queueState;
    public    readonly  SessionId       sessionId;
    internal            SmEventQueue?   eventQueue;

    public   override   string          ToString() => $"session: {sessionId}";

    private static int _sessionSeq;
    
    protected SmClient(SmSessionLoop loop) {
        eventQueue      = loop.eventQueue;
        var id          = Interlocked.Increment(ref _sessionSeq);
        this.sessionId  = new SessionId(id);
    }
    
    /// <summary>
    /// Tries to enqueue an event without blocking.<br/>
    /// If the queue is saturated, the event is dropped and its payload is returned to the pool.<br/>
    /// The method is thread-safe and can be called from any thread.
    /// </summary>
    /// <returns>True if the event was queued; false if it was dropped due to backpressure.</returns>
    protected bool TryEnqueueEvent(LoopEventType type, object? state, Payload payload, int data = 0)
    {
        return eventQueue?.TryEnqueueEventSync(null, default, type, state, payload, data) ?? false;
    }

    /// <summary>
    /// Tries to enqueue an event for a specific client without blocking.<br/>
    /// If the queue is saturated, the event is dropped and its payload is returned to the pool.<br/>
    /// The method is thread-safe and can be called from any thread.
    /// </summary>
    /// <returns>True if the event was queued; false if it was dropped due to backpressure.</returns>
    public bool TryEnqueueClientEvent(LoopEventType type, Payload payload)
    {
        return eventQueue?.TryEnqueueEventSync(this, sessionId, type, null, payload, 0) ?? false;
    }
    
    public void QueueDataAppended(int queueSize)
    {
        queueState.lastAppendedTime = Stopwatch.GetTimestamp();
        queueState.queueSize        = queueSize;
    }
    
    public void QueueDataSent(int queueSize)
    {
        Volatile.Write(ref queueState.lastSentTime, Stopwatch.GetTimestamp());
        Volatile.Write(ref queueState.queueSize,    queueSize);
    }
    
    internal SmSendMessageMode GetSendMode(int maxQueueSize, double maxAgeMs)
    {
        var queueSize = Volatile.Read(ref queueState.queueSize);
        if (queueSize > maxQueueSize) {
            return SmSendMessageMode.DropUnsent;
        }
        var lastSentTime = Volatile.Read(ref queueState.lastSentTime);
        var age = Stopwatch.GetElapsedTime(queueState.lastAppendedTime, lastSentTime).TotalMilliseconds;
        if (age > maxAgeMs) {
            return SmSendMessageMode.DropUnsent;
        }
        return SmSendMessageMode.Queue;
    }
    
    /// <summary>
    /// Important!<br/>
    /// Must be incremented when enqueueing an event to the <see cref="SmSessionLoop"/>.<br/>
    /// Must be decremented when dequeueing an event from the <see cref="SmSessionLoop"/>.<br/> 
    /// </summary>
    private    int     pendingEvents;
    
    internal void IncrementPendingEvents() => Interlocked.Increment(ref pendingEvents);
    internal bool DecrementPendingEvents() => Interlocked.Decrement(ref pendingEvents) == 0;


    public             abstract     UiType          UiType { get; }
    protected internal abstract     ValueTask<int>  SendAsync(SmSendMessage message, CancellationToken cancellationToken);
    protected internal abstract     int             Send(SmSendMessage message);
    protected internal abstract     void            SendCommand(SmSendCommand command);
    protected internal abstract     void            RestoreTerminal();
    
    /// <summary>
    /// Extending classes should null field references to managed types.<br/>
    /// <see cref="SmClient"/> instances may be captured by local variables in async state machines.
    /// E.g. like <c>WebSocketClient</c> created within <c>KestrelHttpServer</c>.
    /// </summary>
    public             abstract  void           Dispose();
}
