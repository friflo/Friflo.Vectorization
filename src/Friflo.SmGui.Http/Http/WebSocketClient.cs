// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Friflo.SmGui.Session;

// ReSharper disable MergeIntoNegatedPattern
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable MergeIntoPattern
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Http;


internal sealed class WebSocketClient : SmClient
{
    private             WebSocket?      webSocket;
    private             WsDoubleBuffer? doubleBuffer;
    private readonly    Channel<bool>   sendChannel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) {
        FullMode = BoundedChannelFullMode.DropOldest
    });

    internal WebSocketClient(WebSocket webSocket, SmSessionLoop loop)
        : base(loop)
    {
        doubleBuffer    = new WsDoubleBuffer(this);
        this.webSocket  = webSocket;
    }
    
    public override void Dispose()
    {
        sendChannel.Writer.TryComplete();
        doubleBuffer?.Dispose();
        doubleBuffer = null;
        webSocket    = null;
    }
    
    public override UiType UiType => UiType.WebGui;

    protected override async ValueTask<int> SendAsync(SmSendMessage message, CancellationToken cancellationToken)
    {
        var ws = webSocket;
        if (ws != null && ws.State == WebSocketState.Open)
        {
            await ws.SendAsync(message.buffer, WebSocketMessageType.Binary, true, cancellationToken);
            return message.Length;
        }
        return 0;
    }
    
    protected override int Send(SmSendMessage message)
    {
        var ws = webSocket;
        if (ws == null || ws.State != WebSocketState.Open) {
            return 0;
        }
        doubleBuffer?.AppendMessage(message);
        
        // Signal the send loop thread-safely without locks or allocations
        sendChannel.Writer.TryWrite(true);

        return message.Length;
    }

    protected override void SendCommand(SmSendCommand command)
    {
        var ws = webSocket;
        if (ws == null || ws.State != WebSocketState.Open) {
            return;
        }
        var buffer = Encoding.UTF8.GetBytes(command.payload);
        _ = ws.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
    }
    protected override void RestoreTerminal() { } // only applicable for terminal emulators

    private async Task FlushPendingBufferAsync(CancellationToken ct)
    {
        var buffer = doubleBuffer!;
        buffer.Swap();

        var ws = webSocket;
        // Send backBuffer asynchronously - OnFrame thread is completely free to write to frontBuffer now
        if (buffer.PendingLength > 0 && ws != null && ws.State == WebSocketState.Open) {
            await ws.SendAsync(buffer.Memory, WebSocketMessageType.Binary, true, ct);
            buffer.Clear();
        }
    }

    // Dedicated loop for receiving incoming messages from client
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
        try
        {
            var ws =  webSocket;
            while (ws != null && ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                }
                catch (Exception)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    break;
                }
                ws = webSocket;
                if (ws != null && result.MessageType == WebSocketMessageType.Close)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text || result.MessageType == WebSocketMessageType.Binary)
                {
                    var payload = new Payload(buffer, result.Count);
                    TryEnqueueClientEvent(LoopEventType.WebsocketInput, payload);
                }
                else
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                // Rent a new buffer for next incoming message
                buffer = ArrayPool<byte>.Shared.Rent(256);
                
                ws = webSocket;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // Dedicated loop for sending outgoing frames/buffers to client
    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            var ws = webSocket;
            while (ws != null && ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                if (doubleBuffer != null && doubleBuffer.HasPendingFrame) {
                    await FlushPendingBufferAsync(cancellationToken);
                }

                // Await signal from sendChannel asynchronously with 0% CPU usage and zero allocations
                await sendChannel.Reader.ReadAsync(cancellationToken);
                ws =  webSocket;
            }
        }
        catch (Exception)
        {
            // Catch socket drops during send
        }
    }

    internal static async Task HandleClientSessionAsync(WebSocketClient client, CancellationToken cancellationToken)
    {
        client.TryEnqueueEvent(LoopEventType.WebsocketConnected, client, default);

        // Run independent receive and send loops concurrently
        Task receiveLoop = client.ReceiveLoopAsync(cancellationToken);
        Task sendLoop    = client.SendLoopAsync   (cancellationToken);

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
            client.TryEnqueueClientEvent(LoopEventType.WebsocketDisconnected, default);
        }
    }
}