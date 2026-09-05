// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

// ReSharper disable ConvertConstructorToMemberInitializers
namespace Friflo.TmGui.TUI.Terminal;


public enum ClientEventType : byte
{
    Connected,
    Disconnected,
    Input
}

public readonly struct ClientEvent
{
    public required     Socket                  Socket  { get; init; }
    public required     ClientEventType         Type    { get; init; }
    public              ReadOnlyMemory<byte>    Payload { get; init; }
}


public sealed class SingleThreadedShardEngine
{
    private readonly    Channel<ClientEvent>            eventChannel;   // Single reader channel guarantees zero-sync single-thread execution
    private readonly    Dictionary<Socket, TuiSession>  sessions;       // Raw non-thread-safe state (accessed exclusively by _shardThread)
    private readonly    FrameBuffer                     frameBuffer;    // shared among all sessions - is accessed single threaded 
    
    public SingleThreadedShardEngine()
    {
        eventChannel    = Channel.CreateUnbounded<ClientEvent>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        sessions        = new Dictionary<Socket, TuiSession>();
        frameBuffer     = new FrameBuffer();
    }
    
    public void Start()
    {
        Thread shardThread = new(RunEventLoop) { IsBackground = true, Name = "ShardLoopThread" };
        shardThread.Start();
    }

    private async ValueTask EnqueueEventAsync(Socket socket, ClientEventType type, ReadOnlyMemory<byte> payload = default)
    {
        await eventChannel.Writer.WriteAsync(new ClientEvent { Socket = socket, Type = type, Payload = payload });
    }

    // Core event loop running strictly on a single thread
    private void RunEventLoop()
    {
        var reader = eventChannel.Reader;
        while (reader.WaitToReadAsync().AsTask().Result)
        {
            while (reader.TryRead(out ClientEvent evt))
            {
                _ = ProcessEvent(evt);
            }
        }
    }
    


    private async ValueTask ProcessEvent(ClientEvent evt)
    {
        switch (evt.Type)
        {
            case ClientEventType.Connected: {
                var newSession = new TuiSession(frameBuffer, TuiColorMode.RGB24);
                var socket = evt.Socket;
                sessions[socket] = newSession;
                
                var sendBuffer = newSession.IterateTui();
                
                _ = await socket.SendAsync(sendBuffer, SocketFlags.None, CancellationToken.None);
                break;
            }
            case ClientEventType.Disconnected:
                sessions.Remove(evt.Socket);
                break;

            case ClientEventType.Input:
                if (sessions.TryGetValue(evt.Socket, out TuiSession? session))
                {
                    ReadOnlySpan<byte> input = evt.Payload.Span;
                    var sendBuffer = session.ProcessInput(input);
                    _ = await evt.Socket.SendAsync(sendBuffer, SocketFlags.None, CancellationToken.None);
                }
                break;
        }
    }
    
    // I/O Loop: Reads raw socket bytes and pushes them into the single-threaded engine queue
    public static async ValueTask HandleClientSessionAsync(Socket socket, SingleThreadedShardEngine engine, CancellationToken cancellationToken)
    {
        // Enable raw mode on client terminal
        await socket.SendAsync(EscapeWrite.EnableRawTuiMode.ToArray(), SocketFlags.None, cancellationToken);

        // Notify engine about new client connection
        await engine.EnqueueEventAsync(socket, ClientEventType.Connected);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(256);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead = await socket.ReceiveAsync(buffer.AsMemory(), SocketFlags.None, cancellationToken);
                if (bytesRead == 0) break;

                ReadOnlyMemory<byte> payload = buffer.AsMemory(0, bytesRead);

                Console.WriteLine($"received [{bytesRead}] text: {Encoding.UTF8.GetString(payload.Span)}");

                // Forward raw input directly to the shard event loop
                await engine.EnqueueEventAsync(socket, ClientEventType.Input, payload);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            await engine.EnqueueEventAsync(socket, ClientEventType.Disconnected);
        }
    }
    
    /*
    // Differential rendering logic without lock overhead
    private static void RenderAndSend(Socket socket, ref PlayerState state, bool isFullRedraw, int oldIndex = 0)
    {
        if (isFullRedraw)
        {
            string fullScreen = "\x1b[2J\x1b[H\x1b[1;36m=== TUI SHARD SYSTEM ===\x1b[0m\r\n\r\n";
            for (int i = 0; i < MenuItems.Length; i++)
                fullScreen += FormatItem(i, i == state.SelectedIndex);

            _ = socket.SendAsync(System.Text.Encoding.UTF8.GetBytes(fullScreen), SocketFlags.None);
            return;
        }

        // Differential update: reposition cursor and update affected lines only
        string diffUpdate = $"\x1b[{6 + oldIndex};1H{FormatItem(oldIndex, false)}" +
                            $"\x1b[{6 + state.SelectedIndex};1H{FormatItem(state.SelectedIndex, true)}";

        _ = socket.SendAsync(System.Text.Encoding.UTF8.GetBytes(diffUpdate), SocketFlags.None);
    }

    private static string FormatItem(int index, bool isSelected) =>
        isSelected ? $"\x1b[1;42;30m> {MenuItems[index]} <\x1b[0m\x1b[K\r\n" : $"  {MenuItems[index]}  \x1b[K\r\n";
    */
}