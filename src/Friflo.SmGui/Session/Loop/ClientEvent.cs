// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Session;


public enum ClientEventType : byte  //  todo  rename -> LoopEventType
{
    // --- client events
    TerminalConnected,
    TerminalDisconnected,
    TerminalInput,
    FrameTick,
    WebsocketConnected,
    WebsocketDisconnected,
    WebsocketInput,
    
    // --- non client events
    SetAppDirty
}

public readonly struct Payload
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

internal readonly struct ClientEvent  //  todo  rename -> LoopEvent
{
    internal required   SmClient?       Client  { get; init; }
    internal required   ClientEventType Type    { get; init; }
    internal required   Payload         Payload { get; init; }
}
