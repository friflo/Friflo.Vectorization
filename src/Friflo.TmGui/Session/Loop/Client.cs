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


public readonly struct TmSendCommand
{
    public readonly string  payload;
    public override string  ToString()      => payload;

    internal TmSendCommand(string payload)  => this.payload = payload;
}


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

[Flags]
public enum UiType
{
    /// <summary>Native App GUI - Vulkan, DX12, Metal, OpenGL (wgpu-native)</summary>
    /// <remarks>Pure native setup for apps or games. Windows, Linux, macOS, Android, iOS. No server / client code included.</remarks>
    NativeGui   =  1,
    /// <summary>Browser GUI (WebSocket) - WebGPU</summary>
    /// <remarks>Requires an TmGui http / WebSocket server streaming the GUI</remarks>
    WebGui      =  2,
    /// <summary>Browser GUI (WebRTC) - WebGPU</summary>
    /// <remarks>Browser connects to another TmGui WebRTC peer which streams the GUI</remarks>
    WebRtcGui   =  4,
    
    /// <summary>Console TUI</summary>
    /// <remarks>Pure native. No server / client is used.</remarks>
    ConsoleTui  =  8,
    /// <summary>Terminal Emulator (TCP) TUI</summary>
    /// <remarks>Requires a TCP server streaming the TUI. Same TUI as in console.</remarks>
    TerminalTui = 16,
    
    Gui = NativeGui | WebGui | WebRtcGui,
    Tui = TerminalTui | ConsoleTui
}

public abstract class TmClient
{
    private             SendQueueState  queueState;
    public   readonly   SessionId       sessionId;

    public   override   string          ToString() => $"session {sessionId}";

    private static int _sessionSeq;
    
    protected TmClient() {
        var id = Interlocked.Increment(ref _sessionSeq);
        this.sessionId = new SessionId(id);
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


    public             abstract     UiType          UiType { get; }
    protected internal abstract     ValueTask<int>  SendAsync(TmSendMessage message, CancellationToken cancellationToken);
    protected internal abstract     int             Send(TmSendMessage message);
    protected internal abstract     void            SendCommand(TmSendCommand command);
    protected internal abstract     void            RestoreTerminal();
    
    /// <summary>
    /// Extending classes should null field references to managed types.<br/>
    /// <see cref="TmClient"/> instances may be captured by local variables in async state machines.
    /// E.g. like <c>WebSocketClient</c> created within <c>KestrelHttpServer</c>.
    /// </summary>
    public             abstract  void           Dispose();
}
