// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Friflo.TmGui.Session;

// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;

internal struct WsSendBuffer
{
    internal    byte[]  data            = new byte[64 * 1024];
    internal    int     pendingLength;
    
    internal void CopyFrom(ReadOnlyMemory<byte> buffer)
    {
        if (data.Length < buffer.Length) {
            data = new byte[buffer.Length];
        }
        buffer.CopyTo(data);
        pendingLength = data.Length;
    }
    
    public WsSendBuffer() { }
}


internal class WebSocketClient : TmClient
{
    private readonly    WebSocket       webSocket;
    private readonly    SemaphoreSlim   sendSignal = new(0, 1);
    private             WsSendBuffer    sendBuffer = new();

    internal WebSocketClient(WebSocket webSocket)
    {
        this.webSocket = webSocket;
    }

    protected override async ValueTask<int> SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (webSocket.State == WebSocketState.Open)
        {
            await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
            return buffer.Length;
        }
        return 0;
    }
    
    protected override int Send(ReadOnlyMemory<byte> data)
    {
        if (webSocket.State != WebSocketState.Open) {
            return 0;
        }
        sendBuffer.CopyFrom(data);

        // Signal background loop to flush frame
        if (sendSignal.CurrentCount == 0) {
            sendSignal.Release();
        }
        return data.Length;
    }

    protected override void RestoreTerminal()
    {
        throw new NotSupportedException();
    }

    private async Task FlushPendingBufferAsync(CancellationToken ct)
    {
        if (sendBuffer.pendingLength > 0 && webSocket.State == WebSocketState.Open) {
            var memoryToSend = new ReadOnlyMemory<byte>(sendBuffer.data, 0, sendBuffer.pendingLength);
            await webSocket.SendAsync(memoryToSend, WebSocketMessageType.Binary, true, ct);
            sendBuffer.pendingLength = 0;
        }
    }

    internal static async Task HandleClientSessionAsync(WebSocketClient client, TmSessionLoop loop, CancellationToken cancellationToken)
    {
        await loop.EnqueueEventAsync(client, ClientEventType.WebsocketConnected, default);

        // Prepare initial receive task
        byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
        Task<WebSocketReceiveResult> receiveTask = client.webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

        while (client.webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            // Wait for incoming WebSocket message OR frame send signal from OnFrame()
            Task completedTask = await Task.WhenAny(receiveTask, client.sendSignal.WaitAsync(cancellationToken));

            if (completedTask == receiveTask)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await receiveTask;
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

                // Rent a new buffer and arm receiveTask for next incoming message
                buffer = ArrayPool<byte>.Shared.Rent(256);
                receiveTask = client.webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            }
            else
            {
                // Frame send signal triggered: flush frame data asynchronously off the OnFrame() thread
                await client.FlushPendingBufferAsync(cancellationToken);
            }
        }
    }
}
