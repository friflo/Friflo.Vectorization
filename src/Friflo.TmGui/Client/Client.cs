// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Friflo.TmGui.Client;


public enum ClientEventType : byte
{
    Connected,
    Disconnected,
    Input
}

public readonly struct ClientEvent
{
    public required     TmClient                Client  { get; init; }
    public required     ClientEventType         Type    { get; init; }
    public              ReadOnlyMemory<byte>    Payload { get; init; }
}

public struct ConnectInfo
{
    public  string[]    args;
    public  TmClient    client;
}

public delegate IGuiView CreateGuiView(ConnectInfo info);


public abstract class TmClient
{
    protected internal abstract  ValueTask<int> SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
}