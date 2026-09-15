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
namespace Friflo.TmGui.Session;


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
    
    protected internal override  int Send(ReadOnlyMemory<byte> buffer)
    {
        outputStream.Write(buffer.Span);
        outputStream.Flush();
        return buffer.Length;
    }
    
    protected internal override void RestoreTerminal()
    {
        Send(TerminalReset);
    }

    // I/O Loop: Reads raw stream bytes and pushes them into the session loop queue
    public static async ValueTask HandleClientSessionAsync(StreamClient client, TmSessionLoop loop, CancellationToken cancellationToken)
    {
        try
        {
            await loop.EnqueueEventAsync(client, ClientEventType.TerminalConnected, default);

            while (!cancellationToken.IsCancellationRequested)
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
                int bytesRead = await client.inputStream.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (bytesRead == 0) break;

                var payload = new Payload(buffer, bytesRead);

                await loop.EnqueueEventAsync(client, ClientEventType.TerminalInput, payload);
            }
        }
        finally
        {
            await loop.EnqueueEventAsync(client, ClientEventType.TerminalDisconnected, default);
        }
    }
}
