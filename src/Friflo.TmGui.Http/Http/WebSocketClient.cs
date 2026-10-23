// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Friflo.TmGui.Session;

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable MergeIntoPattern
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;


internal class WebSocketClient : TmClient
{
    private             WebSocket       webSocket;
    private             WsDoubleBuffer? doubleBuffer;
    private readonly    Channel<bool>   sendChannel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) {
        FullMode = BoundedChannelFullMode.DropOldest
    });

    internal WebSocketClient(WebSocket webSocket)
    {
        doubleBuffer    = new WsDoubleBuffer(this);
        this.webSocket  = webSocket;
    }
    
    public override void Dispose()
    {
        sendChannel.Writer.TryComplete();
        doubleBuffer?.Dispose();
        doubleBuffer = null!;
        webSocket    = null!;
    }
    
    public override TmClientType ClientType => TmClientType.WebGui;

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
        
        // Signal the send loop thread-safely without locks or allocations
        sendChannel.Writer.TryWrite(true);

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

    // Dedicated loop for receiving incoming messages from client
    private static async Task ReceiveLoopAsync(WebSocketClient client, TmSessionLoop loop, CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
        try
        {
            while (client.webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await client.webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
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

                // Rent a new buffer for next incoming message
                buffer = ArrayPool<byte>.Shared.Rent(256);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // Dedicated loop for sending outgoing frames/buffers to client
    private static async Task SendLoopAsync(WebSocketClient client, CancellationToken cancellationToken)
    {
        try
        {
            while (client.webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var doubleBuffer = client.doubleBuffer;
                if (doubleBuffer != null && doubleBuffer.HasPendingFrame) {
                    await client.FlushPendingBufferAsync(cancellationToken);
                }

                // Await signal from sendChannel asynchronously with 0% CPU usage and zero allocations
                await client.sendChannel.Reader.ReadAsync(cancellationToken);
            }
        }
        catch (Exception)
        {
            // Catch socket drops during send
        }
    }

    internal static async Task HandleClientSessionAsync(WebSocketClient client, TmSessionLoop loop, CancellationToken cancellationToken)
    {
        await loop.EnqueueEventAsync(client, ClientEventType.WebsocketConnected, default);

        // Run independent receive and send loops concurrently
        Task receiveLoop = ReceiveLoopAsync(client, loop, cancellationToken);
        Task sendLoop    = SendLoopAsync   (client, cancellationToken);

        try
        {
            // Wait until either loop finishes (due to disconnection or error)
            await Task.WhenAny(receiveLoop, sendLoop);
        }
        catch (Exception)
        {
            // Catch session-level exceptions
        }
        finally
        {
            // Guaranteed to run even if the browser tab was force-closed
            client.Dispose();
            await loop.EnqueueEventAsync(client, ClientEventType.WebsocketDisconnected, default);
        }
    }
}