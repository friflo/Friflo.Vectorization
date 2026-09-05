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

// ReSharper disable ConvertToPrimaryConstructor
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

public interface IGuiView
{
    public void RenderGui(TmBatch batch, int targetWidth, int targetHeight);
}


public struct ConnectInfo
{
    public string[] args;
    public Socket   socket;
}

public delegate IGuiView CreateGuiView(ConnectInfo info);



public sealed class SingleThreadedShardEngine
{
    private readonly    Channel<ClientEvent>            eventChannel;   // Single reader channel guarantees zero-sync single-thread execution
    private readonly    Dictionary<Socket, TuiSession>  sessions;       // Raw non-thread-safe state (accessed exclusively by _shardThread)
    private readonly    FrameBuffer                     frameBuffer;    // shared among all sessions - is accessed single threaded
    private readonly    CreateGuiView                   createGuiView;  // IBatchRenderer factory
    
    public SingleThreadedShardEngine(CreateGuiView createGuiView)
    {
        this.createGuiView  = createGuiView;
        eventChannel        = Channel.CreateUnbounded<ClientEvent>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        sessions            = new Dictionary<Socket, TuiSession>();
        frameBuffer         = new FrameBuffer();
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
                var payload         = evt.Payload.Span;
                var firstLine       = payload.IndexOf((byte)'\n');
                var socket          = evt.Socket;
                var args            = firstLine == -1 ? [] : GetArgs(payload.Slice(0, firstLine));
                var connectInfo     = new ConnectInfo{ socket = socket, args = args };
                var renderer        = createGuiView(connectInfo);
                
                var newSession      = new TuiSession(renderer, frameBuffer, TuiColorMode.RGB24);
                sessions[socket]    = newSession;
                
                var rest        = firstLine == -1 ? default : payload.Slice(firstLine + 1);
                var sendBuffer  = newSession.ProcessInput(rest);
                
                _ = await socket.SendAsync(sendBuffer, SocketFlags.None, CancellationToken.None);
                break;
            }
            case ClientEventType.Disconnected:
                sessions.Remove(evt.Socket);
                break;

            case ClientEventType.Input:
                if (sessions.TryGetValue(evt.Socket, out TuiSession? session))
                {
                    var payload     = evt.Payload.Span;
                    var sendBuffer  = session.ProcessInput(payload);
                    _ = await evt.Socket.SendAsync(sendBuffer, SocketFlags.None, CancellationToken.None);
                }
                break;
        }
    }
    
    private static string[] GetArgs(ReadOnlySpan<byte> payload)
    {
        // Convert initial payload to string (e.g. "--view logs --user 42")
        var commandLine = Encoding.UTF8.GetString(payload).TrimEnd('\r', '\n', '\0');
        
        if (string.IsNullOrWhiteSpace(commandLine)) {
            return [];
        }
        return commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    } 
    
    // I/O Loop: Reads raw socket bytes and pushes them into the single-threaded engine queue
    public static async ValueTask HandleClientSessionAsync(Socket socket, SingleThreadedShardEngine engine, CancellationToken cancellationToken)
    {
        // Enable raw mode on client terminal
        await socket.SendAsync(EscapeWrite.EnableRawTuiMode.ToArray(), SocketFlags.None, cancellationToken);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(256);

        try
        {
            ReadOnlyMemory<byte> initialPayload = default;
            if (socket.Available > 0) {
                int initialBytes = await socket.ReceiveAsync(buffer.AsMemory(), SocketFlags.None, cancellationToken);
                if (initialBytes > 0)
                {
                    initialPayload = buffer.AsMemory(0, initialBytes);
                    Console.WriteLine($"[Handshake] received [{initialBytes}] text: {Encoding.UTF8.GetString(initialPayload.Span)}");
                }
            }

            // Notify engine about new client connection, passing initial payload (if any)
            await engine.EnqueueEventAsync(socket, ClientEventType.Connected, initialPayload);

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
}