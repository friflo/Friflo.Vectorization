// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.Client;


internal enum ClientEventType : byte
{
    TerminalConnected,
    TerminalDisconnected,
    TerminalInput
}

internal readonly struct ClientEvent
{
    internal required   TmClient            Client  { get; init; }
    internal required   ClientEventType     Type    { get; init; }
    internal            Payload             Payload { get; init; }
}

internal readonly struct Payload
{
    private readonly     byte[] buffer;
    private readonly     int    length;
    
    public ReadOnlySpan<byte>   Span => new(buffer, 0, length);

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
    public  string[]    args;
    public  TmClient    client;
}

public delegate IGuiView CreateGuiView(ConnectInfo info);


public abstract class TmClient
{
    protected internal abstract ValueTask<int> SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
}