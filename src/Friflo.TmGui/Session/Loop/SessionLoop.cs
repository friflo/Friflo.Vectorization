// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Friflo.TmGui.Headless;
using Friflo.TmGui.TUI;
using Friflo.TmGui.TUI.VT100;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable ConvertConstructorToMemberInitializers
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;


public sealed partial class TmSessionLoop : IDisposable
{
    public   readonly   AssetResources  resources;

#region private fields
    private  readonly   bool                                isAsync;
    private  readonly   ConcurrentQueue<ClientEvent>        eventQueue;     // used by: sync loop
    private  readonly   AutoResetEvent                      eventReady;     // used by: sync loop
    private  readonly   Channel<ClientEvent>                eventChannel;   // used by: async loop
    private  readonly   Dictionary<TmClient, TmSession>     sessions;       // Raw non-thread-safe state (accessed exclusively by _shardThread)
    private  readonly   CreateGuiView                       createGuiView;  // IBatchRenderer factory
    private  readonly   CancellationTokenSource             cts             = new();
    private             Thread?                             shardThread;
    private             bool                                isDisposed;
    private  readonly   Action                              exitHandler;
    private  readonly   TuiSessionShared                    tuiShared;      // shared among all TuiSession's
    internal readonly   GuiSessionShared                    guiShared;      // shared among all GuiSession's
    internal readonly   CpuBackend                          cpuBackend;    // shared among all sessions

    private const int MaxSyncQueueCapacity = 32;
#endregion
    
    public TmSessionLoop(bool isAsync, TmGuiBackend rootBackend, CreateGuiView createGuiView)
    {
        this.isAsync        = isAsync;
        this.createGuiView  = createGuiView;
        cpuBackend          = new CpuBackend(rootBackend);
        // cpuBackend          = rootBackend as CpuBackend ?? new CpuBackend(rootBackend);  // todo TAG_CPU
        resources           = new AssetResources(cpuBackend);
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
        sessions            = new Dictionary<TmClient, TmSession>();
        exitHandler         = ExitHandler;
        tuiShared           = new TuiSessionShared();
        guiShared           = new GuiSessionShared();
        PosixSignalUtils.AddExitHandler(exitHandler);
    }
    
    public async ValueTask EnqueueEventAsync(TmClient client, ClientEventType type, Payload payload)
    {
        var evt = new ClientEvent { Client = client, Type = type, Payload = payload };
        client.IncrementPendingEvents();
        
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
            client.IncrementPendingEvents();
            // Non-blocking try-write to the channel
            if (eventChannel.Writer.TryWrite(evt)) {
                return true;
            }
            client.DecrementPendingEvents(); // decrement - TryWrite() failed
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
        client.IncrementPendingEvents();
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
    
    private TuiSession CreateTuiSession(ClientEvent evt, bool isSync, out Memory<byte> firstPayload)
    {
        var payload     = evt.Payload;
        var firstLine   = payload.Span.IndexOf((byte)'\n');
        var client      = evt.Client;
        var args        = firstLine == -1 ? [] : GetArgs(payload.Span.Slice(0, firstLine));

        var frameTimer  = new FrameTimer(this, client, 60, isSync);
        var sessionId   = new SessionId(_sessionSeq++);
        var session     = new TuiSession(client, sessionId, tuiShared, frameTimer, cpuBackend.Assets, TuiColorMode.RGB24);

        var sessionInfo = new SessionInfo{ client = client, backend = session.Backend, args = args };
        var guiView     = createGuiView(sessionInfo);
        
        session.guiView = guiView;
        sessions[client]= session;
        var msgStart    = firstLine == -1 ? 0 : firstLine + 1;
        firstPayload    = payload.GetMemory(msgStart);
        return session;
    }

    // protected virtual TmSession CreateGuiSession(TmClient client, bool isSync, out Memory<byte> firstPayload) => throw new NotImplementedException();
    private GuiSession CreateGuiSession(TmClient client, bool isSync, out Memory<byte> firstPayload)
    {
        var frameTimer  = new FrameTimer(this, client, 60, isSync);
        var sessionId   = new SessionId(_sessionSeq++);
        var session     = new GuiSession(sessionId, this, frameTimer);
        
        var sessionInfo = new SessionInfo{ client = client, backend = session.Backend, args = [] };
        var guiView     = createGuiView(sessionInfo);
        
        session.guiView = guiView;
        sessions[client]= session;
        firstPayload    = default;
        return session;
    }
    
    private static int _sessionSeq = 1;
    
    private void DisposeSession(TmSession session)
    {
        var batch = session.Batch;
        batch.frameTimer?.Dispose();
        batch.frameTimer = null;
        resources.RemoveSessionResources(session.sessionId);
    }
}

