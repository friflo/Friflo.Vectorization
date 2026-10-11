// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.Session;


public abstract class TmClient
{
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
    
    protected internal abstract  ValueTask<int> SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
    protected internal abstract  int            Send(ReadOnlyMemory<byte> buffer);
    protected internal abstract  void           RestoreTerminal();
    
    /// <summary>
    /// Extending classes should null field references to managed types.<br/>
    /// <see cref="TmClient"/> instances may be captured by local variables in async state machines.
    /// E.g. like <c>WebSocketClient</c> created within <c>KestrelHttpServer</c>.
    /// </summary>
    public             abstract  void           Dispose();
}
