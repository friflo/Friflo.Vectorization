// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertConstructorToMemberInitializers
namespace Friflo.TmGui.Session;


public class ConsoleClient : TmClient
{
    private readonly Stream inputStream;
    private readonly Stream outputStream;
    
    public ConsoleClient()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        outputStream = Console.OpenStandardOutput();

        if (OperatingSystem.IsWindows()) {
            inputStream = new WinConsoleIn();
        } else {
            inputStream = new AnsiConsoleIn();
        }
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
        if (OperatingSystem.IsWindows()) {
            WinConsoleIn.RestoreConsoleMode();
        }
        
        Send(TerminalReset);
    }

    // I/O Loop: Reads raw stream bytes and pushes them into the session loop queue
    public static async ValueTask HandleClientSessionAsync(ConsoleClient client, TmSessionLoop loop, CancellationToken cancellationToken)
    {
        try
        {
            loop.EnqueueEvent(client, ClientEventType.TerminalConnected, default);

            while (!cancellationToken.IsCancellationRequested)
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
                int bytesRead = await client.inputStream.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (bytesRead == 0) break;

                var payload = new Payload(buffer, bytesRead);

                loop.EnqueueEvent(client, ClientEventType.TerminalInput, payload);
            }
        }
        finally
        {
            loop.EnqueueEvent(client, ClientEventType.TerminalDisconnected, default);
        }
    }
}