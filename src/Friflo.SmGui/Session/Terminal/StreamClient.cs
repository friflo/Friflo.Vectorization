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
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Session;


public class StreamClient : SmClient
{
    private Stream inputStream;
    private Stream outputStream;

    public StreamClient(Stream inputStream, Stream outputStream)
    {
        this.inputStream = inputStream;
        this.outputStream = outputStream;
    }
    
    public override void Dispose() {
        inputStream     = null!;
        outputStream    = null!;
    }
    
    public override UiType UiType => UiType.TerminalTui;
    
    protected internal override async ValueTask<int> SendAsync(SmSendMessage message, CancellationToken cancellationToken)
    {
        await outputStream.WriteAsync(message.buffer, cancellationToken);
        await outputStream.FlushAsync(cancellationToken);
        return message.Length;
    }
    
    protected internal override int Send(SmSendMessage message)
    {
        outputStream.Write(message.buffer.Span);
        outputStream.Flush();
        return message.Length;
    }
    
    protected internal override void SendCommand(SmSendCommand command) { }
    
    protected internal override void RestoreTerminal()
    {
        Send(new SmSendMessage(VT100.TerminalReset));
    }

    // I/O Loop: Reads raw stream bytes and pushes them into the session loop queue
    public static async ValueTask HandleClientSessionAsync(StreamClient client, SmSessionLoop loop, CancellationToken cancellationToken)
    {
        try
        {
            await loop.EnqueueEventAsync(client, LoopEventType.TerminalConnected, default);

            while (!cancellationToken.IsCancellationRequested)
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
                int bytesRead = await client.inputStream.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (bytesRead == 0) break;

                var payload = new Payload(buffer, bytesRead);

                await loop.EnqueueEventAsync(client, LoopEventType.TerminalInput, payload);
            }
        }
        finally
        {
            await loop.EnqueueEventAsync(client, LoopEventType.TerminalDisconnected, default);
        }
    }
}
