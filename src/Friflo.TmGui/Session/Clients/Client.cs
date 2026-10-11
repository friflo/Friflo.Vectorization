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
namespace Friflo.TmGui.Session;

public enum TmSendMessageMode {
    Queue       = 0,
    DropUnsent  = 1
}

public readonly struct TmSendMessage
{
    public readonly ReadOnlyMemory<byte>    buffer;
    public readonly TmSendMessageMode       mode;
    
    public          int                     Length  => buffer.Length;
    public          bool                    IsEmpty => buffer.IsEmpty;

    public override string                  ToString() => $"{mode} [{Length}]";

    [Hide]
    public TmSendMessage(ReadOnlyMemory<byte> buffer) {
        this.buffer = buffer;
        this.mode   = TmSendMessageMode.Queue;
    }
    
    [Hide]
    public TmSendMessage(ReadOnlyMemory<byte> buffer, TmSendMessageMode mode) {
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


public abstract class TmClient
{
    private  SendQueueState    queueState;
    
    protected void QueueDataAppended(int queueSize)
    {
        queueState.lastAppendedTime = Stopwatch.GetTimestamp();
        queueState.queueSize        = queueSize;
    }
    
    protected void QueueDataSent(int queueSize)
    {
        Volatile.Write(ref queueState.lastSentTime, Stopwatch.GetTimestamp());
        Volatile.Write(ref queueState.queueSize,    queueSize);
    }
    
    internal TmSendMessageMode GetSendMode(int maxQueueSize, double maxAgeMs)
    {
        var queueSize = Volatile.Read(ref queueState.queueSize);
        if (queueSize > maxQueueSize) {
            return TmSendMessageMode.DropUnsent;
        }
        var lastSentTime = Volatile.Read(ref queueState.lastSentTime);
        var age = Stopwatch.GetElapsedTime(queueState.lastAppendedTime, lastSentTime).TotalMilliseconds;
        if (age > maxAgeMs) {
            return TmSendMessageMode.DropUnsent;
        }
        return TmSendMessageMode.Queue;
    }
    
    /// <summary>
    /// Important!<br/>
    /// Must be incremented when enqueueing an event to the <see cref="TmSessionLoop"/>.<br/>
    /// Must be decremented when dequeueing an event from the <see cref="TmSessionLoop"/>.<br/> 
    /// </summary>
    private    int     pendingEvents;
    
    internal void IncrementPendingEvents() => Interlocked.Increment(ref pendingEvents);
    internal bool DecrementPendingEvents() => Interlocked.Decrement(ref pendingEvents) == 0;
    
    
    // \x1b[?1006l  Disable SGR mouse tracking
    // \x1b[?1003l  Disable all-motion mouse tracking
    // \x1b[?1002l  Disable button-event mouse tracking
    // \x1b[?1000l  Disable normal mouse tracking
    // \x1b[?2004l  Disable bracketed paste
    // \x1b[?1h     Set Cursor Keys to Application Mode (expected by shell prompts)
    // \x1b[?1049l  Leave alternate screen buffer
    // \x1b[?25h    Show cursor
    // \x1bc        VT100 RIS (Reset to Initial State)
    protected static readonly ReadOnlyMemory<byte> TerminalReset =
        "\x1b[?1006l\x1b[?1003l\x1b[?1002l\x1b[?1000l\x1b[?2004l\x1b[?1h\x1b[?1049l\x1b[?25h\x1bc"u8.ToArray().AsMemory();
    
    protected internal abstract     ValueTask<int>  SendAsync(TmSendMessage message, CancellationToken cancellationToken);
    protected internal abstract     int             Send(TmSendMessage message);
    protected internal abstract     void            RestoreTerminal();
    
    /// <summary>
    /// Extending classes should null field references to managed types.<br/>
    /// <see cref="TmClient"/> instances may be captured by local variables in async state machines.
    /// E.g. like <c>WebSocketClient</c> created within <c>KestrelHttpServer</c>.
    /// </summary>
    public             abstract  void           Dispose();
}
