// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using Friflo.TmGui.Session;

// ReSharper disable InconsistentNaming
// ReSharper disable CanSimplifyStringEscapeSequence
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;

internal abstract class TmSession
{
    protected internal          IGuiView?       guiView;
    internal  readonly          SessionId       sessionId;
    
    public    override          string          ToString()          => $"session: {sessionId}";
    
    protected internal abstract TmGuiBackend    Backend             { get; }
    protected internal abstract TmBatch         Batch               { get; }
    internal           virtual  GuiReplay?      CreateReplay()      => null;
    internal           virtual  void            SendReplayCommands() { }
    
    internal           abstract void            ProcessInput(ReadOnlySpan<byte> input);
    internal           abstract TmSendMessage   IterateUI(in AssetResources resources);
    
    internal TmSession(SessionId sessionId) {
        this.sessionId = sessionId;
    }
}

public enum ClientEventType : byte
{
    TerminalConnected,
    TerminalDisconnected,
    TerminalInput,
    FrameTick,
    WebsocketConnected,
    WebsocketDisconnected,
    WebsocketInput,
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

internal readonly struct ClientEvent
{
    internal required   TmClient        Client  { get; init; }
    internal required   ClientEventType Type    { get; init; }
    internal required   Payload         Payload { get; init; }
}

