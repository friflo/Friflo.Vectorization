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
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Session;

public static class SmConsoleExtensions
{
    public static SmSessionLoop UseConsoleTuiSession(this SmSessionLoop loop, CancellationToken cancellationToken = default)
    {
        var consoleClient = new ConsoleClient(loop);
        _ = consoleClient.HandleConsoleIOAsync(cancellationToken);
        
        return loop;
    }
}

public sealed class ConsoleClient : SmClient
{
    private     Stream? inputStream;
    private     Stream? outputStream;
    private     int     consoleWidth  = -1;
    private     int     consoleHeight = -1;
    
    public ConsoleClient(SmSessionLoop loop) : base(loop)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        outputStream = Console.OpenStandardOutput();

        if (OperatingSystem.IsWindows()) {
            inputStream = new WinConsoleIn();
        } else {
            inputStream = new AnsiConsoleIn();
        }
    }
    
    public override void Dispose()
    {
        inputStream?.Dispose();
        outputStream?.Dispose();
        inputStream     = null;
        outputStream    = null;
    }
    
    public override UiType UiType => UiType.ConsoleTui;
    
    protected internal override async ValueTask<int> SendAsync(SmSendMessage message, CancellationToken cancellationToken)
    {
        var output = outputStream;
        if (output == null) {
            return 0;
        }
        await output.WriteAsync(message.buffer, cancellationToken);
        await output.FlushAsync(cancellationToken);
        return message.Length;
    }
    
    protected internal override int Send(SmSendMessage message)
    {
        var output = outputStream;
        if (output == null) {
            return 0;
        }
        output.Write(message.buffer.Span);
        output.Flush();
        return message.Length;
    }
    
    protected internal override void SendCommand(SmSendCommand command) { }
    
    protected internal override void RestoreTerminal()
    {
        if (OperatingSystem.IsWindows()) {
            WinConsoleIn.RestoreConsoleMode();
        }
        
        Send(new SmSendMessage(VT100.TerminalReset));
    }
    
    private int AppendConsoleSize(byte[] buffer, int start)
    {
        var width   = Console.WindowWidth;
        var height  = Console.WindowHeight;
        if (width == consoleWidth && height == consoleHeight) {
            return 0;
        }
        consoleWidth    = width;
        consoleHeight   = height;
        return VT100.WriteWindowSizeReport(buffer.AsSpan(start, buffer.Length - start), (short)width, (short)height);
    }

    // I/O Loop: Reads raw stream bytes and pushes them into the session loop queue
    public async ValueTask HandleConsoleIOAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
            var length = AppendConsoleSize(buffer, 0);
            var payload = new Payload(buffer, length);
            
            TryEnqueueEvent(LoopEventType.TerminalConnected, this, payload);

            var input = inputStream;
            while (input != null && !cancellationToken.IsCancellationRequested)
            {
                buffer = ArrayPool<byte>.Shared.Rent(256);
                int bytesRead = await input.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (bytesRead == 0) break;
                
                // no need to poll console size on Windows. Size changes are triggered by WINDOW_BUFFER_SIZE_EVENT
                // bytesRead += client.AppendConsoleSize(buffer, bytesRead); 

                payload = new Payload(buffer, bytesRead);

                TryEnqueueClientEvent(LoopEventType.TerminalInput, payload);
                input = inputStream;
            }
        }
        finally
        {
            TryEnqueueClientEvent(LoopEventType.TerminalDisconnected, default);
        }
    }
}