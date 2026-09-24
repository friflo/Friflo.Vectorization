// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
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
namespace Friflo.TmGui.Session;


public sealed partial class TmSessionLoop : IDisposable
{
    private readonly    bool                                isAsync;
    private readonly    ConcurrentQueue<ClientEvent>        eventQueue;     // used by: sync loop
    private readonly    AutoResetEvent                      eventReady;     // used by: sync loop
    private readonly    Channel<ClientEvent>                eventChannel;   // used by: async loop
    private readonly    Dictionary<TmClient, TuiSession>    sessions;       // Raw non-thread-safe state (accessed exclusively by _shardThread)
    private readonly    FrameBuffer                         frameBuffer;    // shared among all sessions - is accessed single threaded
    private readonly    SixelDrawer                         sixelDrawer;    // shared among all sessions
    private readonly    CreateGuiView                       createGuiView;  // IBatchRenderer factory
    private readonly    CancellationTokenSource             cts = new();
    private             Thread?                             shardThread;
    private             bool                                isDisposed;
    private readonly    Action                              exitHandler;

    private const int MaxSyncQueueCapacity = 32;
    
    public TmSessionLoop(bool isAsync, CreateGuiView createGuiView)
    {
        this.isAsync        = isAsync;
        this.createGuiView  = createGuiView;
        if (isAsync) {
            // Bounded channel to enforce non-blocking backpressure via TryWrite
            var options = new BoundedChannelOptions(MaxSyncQueueCapacity) {
                SingleReader = true,
                SingleWriter = false,
                FullMode     = BoundedChannelFullMode.DropWrite // TryWrite fails fast when full
            };
            eventChannel    = Channel.CreateBounded<ClientEvent>(options);
            eventQueue      = null!;
            eventReady      = null!;
        } else {
            eventChannel    = null!;
            eventQueue      = new ConcurrentQueue<ClientEvent>();
            eventReady      = new AutoResetEvent(false);
        }
        sessions            = new Dictionary<TmClient, TuiSession>();
        frameBuffer         = new FrameBuffer();
        sixelDrawer         = new SixelDrawer();
        exitHandler         = ExitHandler;
        PosixSignalUtils.AddExitHandler(exitHandler);
    }
    
    internal async ValueTask EnqueueEventAsync(TmClient client, ClientEventType type, Payload payload)
    {
        var evt = new ClientEvent { Client = client, Type = type, Payload = payload };
        if (isAsync) {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            await eventChannel.Writer.WriteAsync(evt);
            return;
        }
        eventQueue.Enqueue(evt);
        eventReady.Set(); // wake up Thread
    }

    /// <summary>
    /// Tries to enqueue an event without blocking. 
    /// If the queue is saturated, the event is dropped and its payload is returned to the pool.
    /// </summary>
    /// <returns>True if the event was queued; false if it was dropped due to backpressure.</returns>
    internal bool TryEnqueueEvent(TmClient client, ClientEventType type, Payload payload)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        var evt = new ClientEvent { Client = client, Type = type, Payload = payload };

        if (isAsync)
        {
            // Non-blocking try-write to the channel
            if (eventChannel.Writer.TryWrite(evt)) {
                return true;
            }
            // Backpressure triggered: drop event and release memory back to pool
            payload.Return();
            return false;
        }

        // Synchronous path backpressure check
        if (eventQueue.Count >= MaxSyncQueueCapacity) {
            // Queue capacity reached: drop event and release memory back to pool
            payload.Return();
            return false;
        }
        eventQueue.Enqueue(evt);
        eventReady.Set(); // Wake up worker thread
        return true;
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
    
    private TuiSession CreateSession(ClientEvent evt, bool isSync, out Memory<byte> firstPayload)
    {
        var payload     = evt.Payload;
        var firstLine   = payload.Span.IndexOf((byte)'\n');
        var client      = evt.Client;
        var args        = firstLine == -1 ? [] : GetArgs(payload.Span.Slice(0, firstLine));

        var frameTimer  = new FrameTimer(this, evt.Client, 60, isSync);
        var session     = new TuiSession(evt.Client, frameBuffer, frameTimer, sixelDrawer, TuiColorMode.RGB24);

        var sessionInfo = new SessionInfo{ client = client, backend = session.tuiBackend, args = args };
        var guiView     = createGuiView(sessionInfo);
        
        session.guiView = guiView;
        sessions[client]= session;
        var msgStart    = firstLine == -1 ? 0 : firstLine + 1;
        firstPayload    = payload.GetMemory(msgStart);
        return session;
    }
}