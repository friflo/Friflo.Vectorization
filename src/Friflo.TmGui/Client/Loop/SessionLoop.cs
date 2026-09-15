// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Friflo.TmGui.TUI;
using Friflo.TmGui.TUI.VT100;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable ConvertConstructorToMemberInitializers
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Client;


public sealed partial class TmSessionLoop : IDisposable
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
    
    private static string[] GetArgs(ReadOnlySpan<byte> payload)
    {
        // Convert initial payload to string (e.g. "--view logs --user 42")
        var commandLine = Encoding.UTF8.GetString(payload).TrimEnd('\r', '\n', '\0');
        
        if (string.IsNullOrWhiteSpace(commandLine)) {
            return [];
        }
        return commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
    
    private TuiSession CreateSession(ClientEvent evt, out Memory<byte> firstPayload)
    {
        var payload     = evt.Payload;
        var firstLine   = payload.Span.IndexOf((byte)'\n');
        var client      = evt.Client;
        var args        = firstLine == -1 ? [] : GetArgs(payload.Span.Slice(0, firstLine));
        var connectInfo = new ConnectInfo{ client = client, args = args };
        var guiView     = createGuiView(connectInfo);
        
        var session     = new TuiSession(guiView, evt.Client, frameBuffer, TuiColorMode.RGB24);
        sessions[client]= session;
        var msgStart    = firstLine == -1 ? 0 : firstLine + 1;
        firstPayload    = payload.GetMemory(msgStart);
        return session;
    }
}