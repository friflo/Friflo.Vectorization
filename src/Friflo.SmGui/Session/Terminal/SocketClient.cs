// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Session;


public class SocketClient : SmClient
{
    private  Socket socket;
    
    public SocketClient(Socket socket)
    {
        this.socket = socket;
    }
    
    public override void Dispose() {
        socket = null!;
    }
    
    public override UiType UiType => UiType.TerminalTui;
    
    protected internal override async ValueTask<int> SendAsync(SmSendMessage message, CancellationToken cancellationToken)
    {
        return await socket.SendAsync(message.buffer, SocketFlags.None, cancellationToken);
    }
    
    protected internal override int Send(SmSendMessage message)
    {
        // Check if the OS send buffer can accept data immediately (0 microseconds wait time)
        if (!socket.Poll(0, SelectMode.SelectWrite)) {
            return 0;                                       // OS buffer is full -> drop frame instantly to prevent blocking the single thread loop
        }
        return socket.Send(message.buffer.Span, SocketFlags.None);  // Buffer has space -> transmit frame synchronously without blocking
    }
    
    protected internal override void SendCommand(SmSendCommand command) { }
    
    protected internal override void RestoreTerminal()
    {
        Send(new SmSendMessage(VT100.TerminalReset));
    }
    
    
    // I/O Loop: Reads raw socket bytes and pushes them into the session loop queue
    public static async ValueTask HandleClientSessionAsync(SocketClient client, SmSessionLoop loop, CancellationToken cancellationToken)
    {
        var socket = client.socket;
        try
        {
            Payload initialPayload = default;
            if (socket.Available > 0) {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
                int initialBytes = await socket.ReceiveAsync(buffer.AsMemory(), SocketFlags.None, cancellationToken);
                if (initialBytes > 0) {
                    initialPayload = new Payload(buffer, initialBytes);
                }
            }

            // Notify loop about new client connection, passing initial payload (if any)
            await loop.EnqueueEventAsync(LoopEventType.TerminalConnected, client, initialPayload);

            while (!cancellationToken.IsCancellationRequested)
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
                int bytesRead = await socket.ReceiveAsync(buffer.AsMemory(), SocketFlags.None, cancellationToken);
                if (bytesRead == 0) break;

                var payload = new Payload(buffer, bytesRead);

                // Forward raw input directly to the shard event loop
                await loop.EnqueueClientEventAsync(client, LoopEventType.TerminalInput, payload);
            }
        }
        finally
        {
            // ArrayPool<byte>.Shared.Return(buffer);
            await loop.EnqueueClientEventAsync(client, LoopEventType.TerminalDisconnected, default);
        }
    }
}