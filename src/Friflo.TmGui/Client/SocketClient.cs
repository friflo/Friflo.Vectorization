// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.Client;



public class SocketClient : TmClient  
{
    private readonly Socket socket;
    
    public SocketClient(Socket socket)
    {
        this.socket = socket;
    }
    
    protected internal override async ValueTask<int> SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        return await socket.SendAsync(buffer, SocketFlags.None, cancellationToken);
    }
    
    
    // I/O Loop: Reads raw socket bytes and pushes them into the single-threaded engine queue
    public static async ValueTask HandleClientSessionAsync(SocketClient client, SingleThreadedShardEngine engine, CancellationToken cancellationToken)
    {
        var socket = client.socket;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(256);

        try
        {
            ReadOnlyMemory<byte> initialPayload = default;
            if (socket.Available > 0) {
                int initialBytes = await socket.ReceiveAsync(buffer.AsMemory(), SocketFlags.None, cancellationToken);
                if (initialBytes > 0) {
                    initialPayload = buffer.AsMemory(0, initialBytes);
                }
            }

            // Notify engine about new client connection, passing initial payload (if any)
            await engine.EnqueueEventAsync(client, ClientEventType.TerminalConnected, initialPayload);

            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead = await socket.ReceiveAsync(buffer.AsMemory(), SocketFlags.None, cancellationToken);
                if (bytesRead == 0) break;

                var payload = buffer.AsMemory(0, bytesRead);

                // Forward raw input directly to the shard event loop
                await engine.EnqueueEventAsync(client, ClientEventType.TerminalInput, payload);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            await engine.EnqueueEventAsync(client, ClientEventType.TerminalDisconnected);
        }
    }
}