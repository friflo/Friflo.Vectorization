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
namespace Friflo.TmGui.Client;


public class ConsoleClient : TmClient
{
    private readonly Stream inputStream;
    private readonly Stream outputStream;
    
    public ConsoleClient()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        outputStream = Console.OpenStandardOutput();

        // Use native Win32 input stream on Windows, fall back to StandardInput on Unix
        if (OperatingSystem.IsWindows()) {
            // TerminalUtils.EnableRawModeAndVT100(); inputStream = Console.OpenStandardInput();
            inputStream = new Win32ConsoleInputStream();
        } else {
            inputStream = Console.OpenStandardInput();
        }
    }
    
    protected internal override async ValueTask<int> SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        await outputStream.WriteAsync(buffer, cancellationToken);
        await outputStream.FlushAsync(cancellationToken);
        return buffer.Length;
    }
    
    protected internal override void RestoreTerminal()
    {
        Win32ConsoleInputStream.RestoreConsoleMode();
        
        outputStream.Write(TerminalReset.AsMemory().Span);
        outputStream.Flush();
    }

    // I/O Loop: Reads raw stream bytes and pushes them into the single-threaded engine queue
    public static async ValueTask HandleClientSessionAsync(ConsoleClient client, SingleThreadedShardEngine engine, CancellationToken cancellationToken)
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