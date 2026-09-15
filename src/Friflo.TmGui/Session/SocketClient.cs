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
namespace Friflo.TmGui.Session;


public class SocketClient : TmClient
{
    public readonly Socket socket; // is public to enable access to remote info  
    
    public SocketClient(Socket socket)
    {
        this.socket = socket;
    }
    
    protected internal override async ValueTask<int> SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        return await socket.SendAsync(buffer, SocketFlags.None, cancellationToken);
    }
    
    protected internal override  int Send(ReadOnlyMemory<byte> buffer)
    {
        // Check if the OS send buffer can accept data immediately (0 microseconds wait time)
        if (!socket.Poll(0, SelectMode.SelectWrite)) {
            return 0;                                       // OS buffer is full -> drop frame instantly to prevent blocking the single thread loop
        }
        return socket.Send(buffer.Span, SocketFlags.None);  // Buffer has space -> transmit frame synchronously without blocking
    }
    
    protected internal override void RestoreTerminal()
    {
        Send(TerminalReset);
    }
    
    
    // I/O Loop: Reads raw socket bytes and pushes them into the session loop queue
    public static async ValueTask HandleClientSessionAsync(SocketClient client, TmSessionLoop loop, CancellationToken cancellationToken)
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
            await loop.EnqueueEventAsync(client, ClientEventType.TerminalConnected, initialPayload);

            while (!cancellationToken.IsCancellationRequested)
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
                int bytesRead = await socket.ReceiveAsync(buffer.AsMemory(), SocketFlags.None, cancellationToken);
                if (bytesRead == 0) break;

                var payload = new Payload(buffer, bytesRead);

                // Forward raw input directly to the shard event loop
                await loop.EnqueueEventAsync(client, ClientEventType.TerminalInput, payload);
            }
        }
        finally
        {
            // ArrayPool<byte>.Shared.Return(buffer);
            await loop.EnqueueEventAsync(client, ClientEventType.TerminalDisconnected, default);
        }
    }
}