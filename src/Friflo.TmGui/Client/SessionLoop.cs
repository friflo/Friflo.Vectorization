// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Friflo.TmGui.TUI;
using Friflo.TmGui.TUI.VT100;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable ConvertConstructorToMemberInitializers
namespace Friflo.TmGui.Client;


public sealed class TmSessionLoop : IDisposable
{
    private readonly    Channel<ClientEvent>                eventChannel;   // Single reader channel guarantees zero-sync single-thread execution
    private readonly    Dictionary<TmClient, TuiSession>    sessions;       // Raw non-thread-safe state (accessed exclusively by _shardThread)
    private readonly    FrameBuffer                         frameBuffer;    // shared among all sessions - is accessed single threaded
    private readonly    CreateGuiView                       createGuiView;  // IBatchRenderer factory
    private readonly    CancellationTokenSource             cts = new();
    private             Thread?                             shardThread;
    private             bool                                isDisposed;
    private readonly    Action                              exitHandler;

    
    public TmSessionLoop(CreateGuiView createGuiView)
    {
        this.createGuiView  = createGuiView;
        eventChannel        = Channel.CreateUnbounded<ClientEvent>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        sessions            = new Dictionary<TmClient, TuiSession>();
        frameBuffer         = new FrameBuffer();
        exitHandler         = ExitHandler;
        PosixSignalUtils.AddExitHandler(exitHandler);
    }
    
    private void ExitHandler()
    {
        PosixSignalUtils.RemoveExitHandler(exitHandler);
        
        foreach (var client in sessions.Keys) {
            try {
                client.RestoreTerminal();
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch {
                // nothing useful can be done here
            }
        }
    }
    
    internal async ValueTask EnqueueEventAsync(TmClient client, ClientEventType type, Payload payload)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        await eventChannel.Writer.WriteAsync(new ClientEvent { Client = client, Type = type, Payload = payload });
    }
    
    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        // signal all loops to stop
        eventChannel.Writer.TryComplete();
        cts.Cancel();
        
        // wait until ShardLoopThread thread has finished
        shardThread?.Join(timeout: TimeSpan.FromMilliseconds(500));

        cts.Dispose();
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        if (shardThread != null)        {
            throw new InvalidOperationException("Engine is already running.");
        }
        shardThread = new Thread(RunThreadLoop) {
            IsBackground = true,
            Name = "ShardLoopThread"
        };
        shardThread.Start();
    }

    private void RunThreadLoop()
    {
        try {
            RunEventLoopAsync(cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Expected behavior if canceled
        }
        catch (Exception ex)
        {
            Debug.Fail($"Critical failure in ShardLoopThread: {ex}");
        }
    }

    private async Task RunEventLoopAsync(CancellationToken cancellationToken)
    {
        var reader = eventChannel.Reader;

        while (await reader.WaitToReadAsync(cancellationToken))
        {
            while (reader.TryRead(out ClientEvent evt))
            {
                await ProcessEventAsync(evt);
            }
        }
    }

    private async ValueTask ProcessEventAsync(ClientEvent evt)
    {
        try {
            switch (evt.Type)
            {
                case ClientEventType.TerminalConnected: {
                    var payload         = evt.Payload;
                    var firstLine       = payload.Span.IndexOf((byte)'\n');
                    var client          = evt.Client;
                    var args            = firstLine == -1 ? [] : GetArgs(payload.Span.Slice(0, firstLine));
                    var connectInfo     = new ConnectInfo{ client = client, args = args };
                    var guiView         = createGuiView(connectInfo);
                    
                    var newSession      = new TuiSession(guiView, evt.Client, frameBuffer, TuiColorMode.RGB24);
                    sessions[client]    = newSession;
                    

                    var initialMessage = newSession.StartSession();
                    await client.SendAsync(initialMessage, CancellationToken.None);
                    
                    var rest            = firstLine == -1 ? payload.Span : payload.Span.Slice(firstLine + 1);
                    var sendBuffer  = newSession.ProcessInput(rest);
                    
                    await client.SendAsync(sendBuffer, CancellationToken.None);
                    break;
                }
                case ClientEventType.TerminalDisconnected:
                    sessions.Remove(evt.Client);
                    break;

                case ClientEventType.TerminalInput:
                    if (sessions.TryGetValue(evt.Client, out TuiSession? session))
                    {
                        var payload     = evt.Payload.Span;
                        var sendBuffer  = session.ProcessInput(payload);
                        await evt.Client.SendAsync(sendBuffer, CancellationToken.None);
                    }
                    break;
            }
        } catch (Exception e) {
            Debug.Fail(e.ToString());
        } finally {
            evt.Payload.Return();
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