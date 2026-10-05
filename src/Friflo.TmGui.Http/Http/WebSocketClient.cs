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
    internal byte[] data = new byte[64 * 1024];
    internal int pendingLength;
    
    internal void AppendFrom(ReadOnlyMemory<byte> buffer)
    {
        int newLength = pendingLength + buffer.Length;
        if (data.Length < newLength) {
            byte[] newBuffer = new byte[Math.Max(data.Length * 2, newLength)];
            Array.Copy(data, 0, newBuffer, 0, pendingLength);
            data = newBuffer;
        }
        buffer.CopyTo(data.AsMemory(pendingLength));
        pendingLength = newLength;
    }
    
    public WsSendBuffer() { }
}

internal class WebSocketClient : TmClient
{
    private readonly    WebSocket       webSocket;
    private readonly    object          bufferLock = new();
    
    // Lock-free flag for signaling: 0 = no data, 1 = frame pending
    private             int             hasPendingFrame;
    
    // Double-buffering to prevent race conditions between OnFrame thread and Kestrel flush
    private             WsSendBuffer    frontBuffer = new();
    private             WsSendBuffer    backBuffer  = new();

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

        lock (bufferLock)
        {
            frontBuffer.AppendFrom(data);
            Volatile.Write(ref hasPendingFrame, 1);
        }

        return data.Length;
    }

    protected override void RestoreTerminal()
    {
        throw new NotSupportedException();
    }

    private async Task FlushPendingBufferAsync(CancellationToken ct)
    {
        // Swap buffers quickly under lock to minimize OnFrame thread wait time
        lock (bufferLock)
        {
            if (frontBuffer.pendingLength == 0) {
                Volatile.Write(ref hasPendingFrame, 0);
                return;
            }

            // Swap front and back buffer structs
            (frontBuffer, backBuffer) = (backBuffer, frontBuffer);
            
            // Clear pending length on new frontBuffer so it is ready for the next frame
            frontBuffer.pendingLength = 0;
            Volatile.Write(ref hasPendingFrame, 0);
        }

        // Send backBuffer asynchronously - OnFrame thread is completely free to write to frontBuffer now
        if (backBuffer.pendingLength > 0 && webSocket.State == WebSocketState.Open) {
            var memoryToSend = new ReadOnlyMemory<byte>(backBuffer.data, 0, backBuffer.pendingLength);
            await webSocket.SendAsync(memoryToSend, WebSocketMessageType.Binary, true, ct);
            backBuffer.pendingLength = 0;
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
            // Wait briefly or check for pending frame signal without WaitAsync allocations
            if (Volatile.Read(ref client.hasPendingFrame) == 1)
            {
                await client.FlushPendingBufferAsync(cancellationToken);
            }

            if (receiveTask.IsCompleted)
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

            // Yield control briefly to avoid CPU spinning when idle
            await Task.Delay(1, cancellationToken);
        }
    }
}