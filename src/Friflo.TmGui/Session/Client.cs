// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable CanSimplifyStringEscapeSequence
// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.Session;


internal abstract class TmSession
{
    internal virtual GuiReplay? CreateReplay()      => null;
    internal virtual void       SendReplayCommands() { }
}

internal enum ClientEventType : byte
{
    TerminalConnected,
    TerminalDisconnected,
    TerminalInput,
    FrameTick
}

internal readonly struct ClientEvent
{
    internal required   TmClient            Client  { get; init; }
    internal required   ClientEventType     Type    { get; init; }
    internal required   Payload             Payload { get; init; }
}

internal readonly struct Payload
{
    private readonly     byte[] buffer;
    private readonly     int    length;
    
    public ReadOnlySpan<byte>   Span                    => new(buffer, 0,     length);
    public Memory<byte>         GetMemory(int start)    => new(buffer, start, length - start);

    public Payload(byte[] buffer, int length) {
        this.buffer = buffer;
        this.length = length;
    }
    
    public void Return()
    {
        if (buffer == null) return;
        ArrayPool<byte>.Shared.Return(buffer);
    }
}

public struct ConnectInfo
{
    public  string[]        args;
    public  TmClient        client;
    public  TmGuiBackend    backend;
}

public delegate IGuiView CreateGuiView(ConnectInfo info);


public abstract class TmClient
{
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
}