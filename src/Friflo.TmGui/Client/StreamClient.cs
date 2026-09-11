// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.Client;




public class StreamClient : TerminalClient
{
    private readonly Stream inputStream;
    private readonly Stream outputStream;

    public StreamClient(Stream inputStream, Stream outputStream)
    {
        this.inputStream = inputStream;
        this.outputStream = outputStream;
    }
    
    protected internal override async ValueTask<int> SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        await outputStream.WriteAsync(buffer, cancellationToken);
        await outputStream.FlushAsync(cancellationToken);
        return buffer.Length;
    }

    // I/O Loop: Reads raw stream bytes and pushes them into the single-threaded engine queue
    public static async ValueTask HandleClientSessionAsync(StreamClient client, SingleThreadedShardEngine engine, CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(256);

        try
        {
            await engine.EnqueueEventAsync(client, ClientEventType.Connected, ReadOnlyMemory<byte>.Empty);

            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead = await client.inputStream.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (bytesRead == 0) break;

                ReadOnlyMemory<byte> payload = buffer.AsMemory(0, bytesRead);

                await engine.EnqueueEventAsync(client, ClientEventType.Input, payload);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            await engine.EnqueueEventAsync(client, ClientEventType.Disconnected);
        }
    }
}
