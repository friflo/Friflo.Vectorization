// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.Client;


public class StreamClient : TmClient
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

        try
        {
            await engine.EnqueueEventAsync(client, ClientEventType.TerminalConnected, default);

            while (!cancellationToken.IsCancellationRequested)
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
                int bytesRead = await client.inputStream.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (bytesRead == 0) break;

                var payload = new Payload(buffer, bytesRead);

                await engine.EnqueueEventAsync(client, ClientEventType.TerminalInput, payload);
            }
        }
        finally
        {
            await engine.EnqueueEventAsync(client, ClientEventType.TerminalDisconnected, default);
        }
    }
}
