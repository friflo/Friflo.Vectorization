// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
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
            try {
                webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, CancellationToken.None)
                         .GetAwaiter()
                         .GetResult();
                return buffer.Length;
            } catch (WebSocketException e) {
                Console.WriteLine(e);
            }
        }
        return 0;
    }

    protected internal override void RestoreTerminal()
    {
        throw new NotSupportedException();
    }

    internal static async Task HandleClientSessionAsync(WebSocketClient client, TmSessionLoop loop, CancellationToken cancellationToken)
    {
        await loop.EnqueueEventAsync(client, ClientEventType.WebsocketConnected, default);

        while (client.webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
            WebSocketReceiveResult result;
            try
            {
                result = await client.webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            }
            catch (WebSocketException)
            {
                ArrayPool<byte>.Shared.Return(buffer);
                break;
            }
            if (result.MessageType == WebSocketMessageType.Close)
            {
                ArrayPool<byte>.Shared.Return(buffer);
                await client.webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                break;
            }
            if (result.MessageType == WebSocketMessageType.Text || result.MessageType == WebSocketMessageType.Binary)
            {
                var payload = new Payload(buffer, result.Count);
                await loop.EnqueueEventAsync(client, ClientEventType.WebsocketInput, payload);
            }
            else
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
