// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable ConvertConstructorToMemberInitializers
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.TUI.Terminal;


public sealed class SingleThreadedShardEngine
{
    private readonly    Channel<ClientEvent>                    eventChannel;   // Single reader channel guarantees zero-sync single-thread execution
    private readonly    Dictionary<TerminalClient, TuiSession>  sessions;       // Raw non-thread-safe state (accessed exclusively by _shardThread)
    private readonly    FrameBuffer                             frameBuffer;    // shared among all sessions - is accessed single threaded
    private readonly    CreateGuiView                           createGuiView;  // IBatchRenderer factory
    
    public SingleThreadedShardEngine(CreateGuiView createGuiView)
    {
        this.createGuiView  = createGuiView;
        eventChannel        = Channel.CreateUnbounded<ClientEvent>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        sessions            = new Dictionary<TerminalClient, TuiSession>();
        frameBuffer         = new FrameBuffer();
    }
    
    public void Start()
    {
        Thread shardThread = new(RunEventLoop) { IsBackground = true, Name = "ShardLoopThread" };
        shardThread.Start();
    }

    internal async ValueTask EnqueueEventAsync(TerminalClient client, ClientEventType type, ReadOnlyMemory<byte> payload = default)
    {
        await eventChannel.Writer.WriteAsync(new ClientEvent { Client = client, Type = type, Payload = payload });
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
                var client          = evt.Client;
                var args            = firstLine == -1 ? [] : GetArgs(payload.Slice(0, firstLine));
                var connectInfo     = new ConnectInfo{ client = client, args = args };
                var guiView         = createGuiView(connectInfo);
                
                var newSession      = new TuiSession(guiView, frameBuffer, TuiColorMode.RGB24);
                sessions[client]    = newSession;
                
                var rest            = firstLine == -1 ? default : payload.Slice(firstLine + 1);
                
                newSession.StartSession();
                var sendBuffer  = newSession.ProcessInput(rest);
                
                _ = await client.SendAsync(sendBuffer, CancellationToken.None);
                break;
            }
            case ClientEventType.Disconnected:
                sessions.Remove(evt.Client);
                break;

            case ClientEventType.Input:
                if (sessions.TryGetValue(evt.Client, out TuiSession? session))
                {
                    var payload     = evt.Payload.Span;
                    var sendBuffer  = session.ProcessInput(payload);
                    _ = await evt.Client.SendAsync(sendBuffer, CancellationToken.None);
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
}