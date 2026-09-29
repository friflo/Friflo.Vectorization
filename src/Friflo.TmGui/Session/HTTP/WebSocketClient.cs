// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session.HTTP;


internal class WebSocketClient : TmClient
{
    private readonly WebSocket webSocket;

    internal WebSocketClient(WebSocket webSocket)
    {
        this.webSocket = webSocket;
    }

    protected internal override async ValueTask<int> SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (webSocket.State == WebSocketState.Open)
        {
            await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
            return buffer.Length;
        }
        return 0;
    }
    
    protected internal override int Send(ReadOnlyMemory<byte> buffer)
    {
        if (webSocket.State == WebSocketState.Open) {
            webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, CancellationToken.None)
                     .GetAwaiter()
                     .GetResult();
            return buffer.Length;
        }
        return 0;
    }

    protected internal override void RestoreTerminal()
    {
        throw new NotSupportedException();
    }

    internal async ValueTask<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (webSocket.State != WebSocketState.Open) return 0;

        var result = await webSocket.ReceiveAsync(buffer, cancellationToken);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
            return 0;
        }

        return result.Count;
    }

    internal static async Task HandleClientSessionAsync(WebSocketClient client, TmSessionLoop loop, CancellationToken cancellationToken)
    {
        await loop.EnqueueEventAsync(client, ClientEventType.WebsocketConnected, default);
    }
}
