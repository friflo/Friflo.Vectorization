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
using Friflo.TmGui.TUI.Session;

// ReSharper disable InlineTemporaryVariable
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable ConvertConstructorToMemberInitializers
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;


/// <summary>
/// Manages concurrent client sessions (such as web browsers, remote terminals, or consoles), 
/// processing queued user inputs and coordinating on-demand frame rendering and transmission 
/// exclusively for clients with pending input or dirty states.
/// </summary>
/// <remarks>
/// Handles all client sessions on the main thread, drastically simplifying application development. 
/// This enables lock-free execution since application and session state are always accessed from the same thread.<br/>
/// <br/>
/// The <see cref="CreateGuiView"/> delegate passed to the <see cref="TmSessionLoop"/> constructor must return an instance implementing <see cref="IGuiView"/>.<br/>
/// If a client has pending input or a dirty session, a new frame is rendered via <see cref="IGuiView.RenderGui"/> and transmitted to the client.<br/>
/// <br/>
/// Use <see cref="RunSync"/> or <see cref="RunAsync"/> to let the loop run automatically (ideal for console and terminal applications).<br/>
/// Alternatively, call <see cref="IterateSessions"/> periodically if your application manages its own custom event loop, such as a game loop.
/// </remarks>
public sealed partial class TmSessionLoop : IDisposable
{
    public   readonly   AssetResources  resources;
    public              string?         appId;
    public              bool            UseSecureSessionGuids { get; init; }

#region private fields
    private  readonly   bool                                isAsync;
    private  readonly   ConcurrentQueue<ClientEvent>        eventQueue;     // used by: sync loop
    private  readonly   AutoResetEvent                      eventReady;     // used by: sync loop
    private  readonly   Channel<ClientEvent>?               eventChannel;   // used by: async loop
    private  readonly   Dictionary<SessionId, TmSession>    sessions;       // Raw non-thread-safe state (accessed exclusively by _shardThread)
    private  readonly   List<TmSession>                     sessionBuffer   = [];
    private  readonly   CreateGuiView                       createGuiView;  // IBatchRenderer factory
    private  readonly   CancellationTokenSource             cts             = new();
    private             Thread?                             shardThread;
    private             bool                                isDisposed;
    private  readonly   Action                              exitHandler;
    internal readonly   TuiSessionShared                    tuiShared;      // shared among all TuiSession's
    internal readonly   GuiSessionShared                    guiShared;      // shared among all GuiSession's
    internal readonly   CpuBackend                          cpuBackend;     // shared among all sessions

    private const int MaxSyncQueueCapacity = 32;
#endregion
    
    public TmSessionLoop(bool isAsync, CpuBackend cpuBackend, CreateGuiView createGuiView)
    {
        this.isAsync        = isAsync;
        this.createGuiView  = createGuiView;
        this.cpuBackend     = cpuBackend;  // TAG_CPU
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
            eventQueue      = new ConcurrentQueue<ClientEvent>();
            eventReady      = new AutoResetEvent(false);
        }
        sessions            = new Dictionary<SessionId, TmSession>();
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
            await eventChannel!.Writer.WriteAsync(evt);
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
            if (eventChannel!.Writer.TryWrite(evt)) {
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
        
        foreach (var session in sessions.Values) {
            try {
                session.client.RestoreTerminal();
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch {
                // nothing useful can be done here
            }
        }
    }
    
    public void Dispose()
    {
        if (isDisposed) {
            return;
        }
        isDisposed = true;
        
        foreach (var session in sessions.Values) {
            session.client.Dispose();
        }

        // signal all loops to stop
        eventChannel?.Writer.TryComplete();
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
        var payload         = evt.Payload;
        var firstLine       = payload.Span.IndexOf((byte)'\n');
        var client          = evt.Client;
        var args            = firstLine == -1 ? [] : GetArgs(payload.Span.Slice(0, firstLine));

        var loopTimer       = new LoopTimer(this, client, 60, isSync);
        var keys            = new SessionKeys(client.sessionId, default);
        var session         = new TuiSession(client, keys, this, loopTimer, TuiColorMode.RGB24);
        var sessionClient   = new SessionClient(this, session, args, client, session.Backend, loopTimer);
        var guiView         = createGuiView(sessionClient);
        
        session.guiView = guiView;
        sessions[client.sessionId] = session;
        var msgStart    = firstLine == -1 ? 0 : firstLine + 1;
        firstPayload    = payload.GetMemory(msgStart);
        return session;
    }

    // protected virtual TmSession CreateGuiSession(TmClient client, bool isSync, out Memory<byte> firstPayload) => throw new NotImplementedException();
    private GuiSession CreateGuiSession(TmClient client, bool isSync, out Memory<byte> firstPayload)
    {
        var loopTimer       = new LoopTimer(this, client, 60, isSync);
        var sessionId       = client.sessionId;
        var sessionGuid     = UseSecureSessionGuids ? new SessionGuid(Guid.NewGuid()) : new SessionGuid(sessionId.value);
        var keys            = new SessionKeys(sessionId, sessionGuid);
        var session         = new GuiSession(client, keys, this, loopTimer);
        var sessionClient   = new SessionClient(this, session, [], client, session.Backend, loopTimer);
        var guiView         = createGuiView(sessionClient);
        
        session.guiView = guiView;
        sessions[client.sessionId] = session;
        firstPayload    = default;
        return session;
    }

    
    private void DisposeSession(TmSession session)
    {
        var batch = session.Batch;
        batch.loopTimer.Dispose();
        resources.RemoveSessionResources(session.keys.guid);
    }

    /// <summary>
    /// Enqueues a redraw (<see cref="IGuiView.RenderGui"/>) for all clients of the app.
    /// </summary>
    /// <remarks>
    /// Applies to native GUI window, console TUI, browser clients, and terminal emulators.<br/>
    /// The method is threadsafe and can be called from any thread.
    /// </remarks>
    public void SetAppDirty()
    {
        var buffer = sessionBuffer;
        buffer.Clear();
        buffer.AddRange(sessions.Values);
        
        foreach (var session in buffer) {
            if (session.SetDirty()) {
                continue;
            }
            TryEnqueueEvent(session.client, ClientEventType.FrameTick, default);
        }
        buffer.Clear();
    }
    
    /// <summary>
    /// Enqueues a redraw (<see cref="IGuiView.RenderGui"/>) for all clients of a specific session.
    /// </summary>
    /// <remarks>
    /// Applies to native GUI window, console TUI, browser clients, and terminal emulators.<br/>
    /// The method is threadsafe and can be called from any thread.
    /// </remarks>
    public void SetSessionDirty(SessionId sessionId)
    {
        var buffer = sessionBuffer;
        buffer.Clear();
        buffer.AddRange(sessions.Values);
        
        foreach (var session in buffer) {
            if (session.keys.id != sessionId) {
                continue;
            }
            if (session.SetDirty()) {
                continue;
            }
            TryEnqueueEvent(session.client, ClientEventType.FrameTick, default);
        }
        buffer.Clear();
    }
}

