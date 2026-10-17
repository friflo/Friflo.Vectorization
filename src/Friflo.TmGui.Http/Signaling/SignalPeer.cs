// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;


public sealed class TmSignalPeer
{
    public              TmGuiApp?   guiApp;       // the GUI app a client wants to connect
    public   readonly   DateTime    registered;
    public   readonly   string      connection;
    internal readonly   WebSocket   webSocket;
    
    public   override   string      ToString() => $"{(guiApp == null ? "App" : "Client")}  conn: {connection}";
    

    internal TmSignalPeer(WebSocket webSocket, string connection)
    {
        this.webSocket  = webSocket;
        this.connection = connection;
        registered      = DateTime.Now;
    }

    internal async Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        if (webSocket.State == WebSocketState.Open)
        {
            await webSocket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
        }
    }
}