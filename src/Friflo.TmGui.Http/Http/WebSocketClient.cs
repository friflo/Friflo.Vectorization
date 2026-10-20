// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Friflo.TmGui.Session;

// ReSharper disable MergeIntoPattern
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;


internal class WebSocketClient : TmClient
{
    private     WebSocket                   webSocket;
    private     WsDoubleBuffer?             doubleBuffer;
    private     TaskCompletionSource<bool>  sendTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal WebSocketClient(WebSocket webSocket)
    {
        doubleBuffer    = new WsDoubleBuffer(this);
        this.webSocket  = webSocket;
    }
    
    public override void Dispose()
    {
        doubleBuffer?.Dispose();
        doubleBuffer = null!;
        webSocket    = null!;
    }

    protected override async ValueTask<int> SendAsync(TmSendMessage message, CancellationToken cancellationToken)
    {
        if (webSocket.State == WebSocketState.Open)
        {
            await webSocket.SendAsync(message.buffer, WebSocketMessageType.Binary, true, cancellationToken);
            return message.Length;
        }
        return 0;
    }
    
    protected override int Send(TmSendMessage message)
    {
        if (webSocket.State != WebSocketState.Open) {
            return 0;
        }
        doubleBuffer?.AppendMessage(message);
        
        // Signal the session loop thread-safely that new outgoing data is ready
        lock (sendTcs) {
            sendTcs.TrySetResult(true);
        }

        return message.Length;
    }

    protected override void SendCommand(TmSendCommand command)
    {
        if (webSocket.State != WebSocketState.Open) {
            return;
        }
        var buffer = Encoding.UTF8.GetBytes(command.payload);
        _ = webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
    }
    protected override void RestoreTerminal() { } // only applicable for terminal emulators

    private async Task FlushPendingBufferAsync(CancellationToken ct)
    {
        var buffer = doubleBuffer!;
        buffer.Swap();

        // Send backBuffer asynchronously - OnFrame thread is completely free to write to frontBuffer now
        if (buffer.PendingLength > 0 && webSocket.State == WebSocketState.Open) {
            await webSocket.SendAsync(buffer.Memory, WebSocketMessageType.Binary, true, ct);
            buffer.Clear();
        }
    }

    internal static async Task HandleClientSessionAsync(WebSocketClient client, TmSessionLoop loop, CancellationToken cancellationToken)
    {
        await loop.EnqueueEventAsync(client, ClientEventType.WebsocketConnected, default);

        // Prepare initial receive task
        byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
        Task<WebSocketReceiveResult> receiveTask = client.webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

        try
        {
            while (client.webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                // Flush any pending frame before waiting
                var doubleBuffer = client.doubleBuffer;
                if (doubleBuffer != null && doubleBuffer.HasPendingFrame) {
                    await client.FlushPendingBufferAsync(cancellationToken);
                }

                // Grab the current send signal task thread-safely
                Task sendSignalTask;
                lock (client.sendTcs) {
                    sendSignalTask = client.sendTcs.Task;
                }

                // Wait for either an incoming receive message OR a send signal
                Task completedTask = await Task.WhenAny(receiveTask, sendSignalTask);

                // If woken up by send signal, reset the TCS and loop back immediately to flush
                if (completedTask == sendSignalTask)
                {
                    lock (client.sendTcs) {
                        client.sendTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    }
                    continue;
                }

                if (receiveTask.IsCompleted)
                {
                    WebSocketReceiveResult result;
                    try
                    {
                        result = await receiveTask;
                    }
                    catch (Exception)
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
            }
        }
        catch (Exception)
        {
            // Catch socket drops / tab closes during Task.Delay or ReceiveAsync
        }
        finally
        {
            // Guaranteed to run even if the browser tab was force-closed
            await loop.EnqueueEventAsync(client, ClientEventType.WebsocketDisconnected, default);
        }
    }
}