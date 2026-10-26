// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Session;


public enum LoopEventType
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
    private readonly     byte[] buffer;     // 8 bytes
    private readonly     int    length;     // 4 bytes
    
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
    internal required   SmClient?       Client  { get; init; }  //  8 bytes
    internal required   LoopEventType   Type    { get; init; }  //  4 bytes
    internal required   Payload         Payload { get; init; }  // 16 bytes
}
