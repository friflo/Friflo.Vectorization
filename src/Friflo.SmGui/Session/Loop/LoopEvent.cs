// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Session;


public enum LoopEventType
{
    // --- session events
    TerminalDisconnected,
    TerminalInput,
    FrameTick,
    WebsocketDisconnected,
    WebsocketInput,
    
    // --- non client events
    TerminalConnected,
    WebsocketConnected,
    SetAppDirty
}

public readonly struct Payload
{
    internal readonly   byte[]  buffer;     // 8 bytes
    internal readonly   int     length;     // 4 bytes
    
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

internal readonly struct LoopEvent
{
    internal readonly   SessionId       sessionId;  //  4 bytes
    internal readonly   LoopEventType   type;       //  4 bytes
    internal readonly   object?         state;      //  8 bytes
    private  readonly   byte[]          buffer;     //  8 bytes
    private  readonly   int             length;     //  4 bytes
    internal readonly   int             data;       //  4 bytes

    internal            Payload         Payload     => new(buffer, length);

    public override     string          ToString()  => sessionId.id == 0 ? type.ToString() : $"{type}  session: {sessionId}";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal LoopEvent(SessionId sessionId, LoopEventType type, object? state, Payload payload, int data)
    {
        this.sessionId  = sessionId;
        this.type       = type;
        this.state      = state;
        buffer          = payload.buffer;
        length          = payload.length;
        this.data       = data;
    }
}
