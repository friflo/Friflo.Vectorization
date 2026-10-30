// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Friflo.SmGui.Headless;
using Friflo.SmGui.TUI;
using Friflo.SmGui.TUI.Session;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable InlineTemporaryVariable
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable ConvertConstructorToMemberInitializers
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Session;

public enum SessionsChangedType
{
    Added,
    Removed,
}

public readonly struct SessionsChangedArgs
{
    public readonly SessionId           sessionId;
    public readonly SessionsChangedType type;

    public override string              ToString() => $"sessionId: {sessionId}  {type}";

    public SessionsChangedArgs(SessionId sessionId, SessionsChangedType type) {
        this.sessionId  = sessionId;
        this.type       = type;
    }
}

public delegate void SessionsChanged(SessionsChangedArgs args);

/// <summary>
/// Manages concurrent client sessions (such as web browsers, remote terminals, or consoles),
/// processing queued user inputs and coordinating on-demand frame rendering and transmission
/// exclusively for clients with pending input or dirty states.
/// </summary>
/// <remarks>
/// Handles all client sessions on the main thread, simplifying application development and enabling lock-free access to application and session state.<br/>
/// <br/>
/// The <see cref="CreateGuiView"/> delegate passed to the <see cref="SmSessionLoop"/> constructor must return an instance implementing <see cref="IGuiView"/>.<br/>
/// If a client has pending input or a dirty session, a new frame is rendered via <see cref="IGuiView.RenderView"/> and transmitted to the client.<br/>
/// <br/>
/// Use <see cref="Run"/> to let the loop run automatically (ideal for console and terminal applications).<br/>
/// Alternatively, call <see cref="IterateSessions"/> periodically if your application manages its own custom event loop, such as a game loop.
/// </remarks>
public sealed partial class SmSessionLoop : IDisposable
{
    public   readonly   AssetResources      resources;
    public              string?             appId;
    public              bool                UseSecureSessionGuids   { get; init; }
    public              int                 SessionCount            => sessions.Count;
    public   event      SessionsChanged?    SessionsChanged;

#region private fields
//  private  readonly   bool                                        isAsync;
    internal readonly   SmEventQueue                                eventQueue;
    private  readonly   ConcurrentQueue<LoopEvent>                  queue;              // used by: sync loop
    private  readonly   AutoResetEvent                              eventReady;         // used by: sync loop
//  private  readonly   Channel<LoopEvent>?                         eventChannel;       // was used by: async loop
    private  readonly   Dictionary<SessionId, SmSession>            sessions            = new(); // non-thread-safe state
    private  readonly   ConcurrentDictionary<SessionId, SmSession>  concurrentSessions  = new(); // thread-safe - used only for SetSessionDirty(SessionId) 
    internal readonly   int                                         sessionThreadId;
    private  readonly   CreateGuiView                               createGuiView;      // IGuiView factory
    private  readonly   CancellationTokenSource                     cts                 = new();
    private             bool                                        isDisposed;
    private  readonly   Action                                      exitHandler;
    internal readonly   TuiSessionShared                            tuiShared;          // shared among all TuiSession's
    internal readonly   GuiSessionShared                            guiShared;          // shared among all GuiSession's
    internal readonly   CpuBackend                                  cpuBackend;         // shared among all sessions
#endregion
    
    public SmSessionLoop(CpuBackend cpuBackend, CreateGuiView createGuiView)
    {
        // this.isAsync        = isAsync;
        this.createGuiView  = createGuiView;
        this.cpuBackend     = cpuBackend;  // TAG_CPU
        resources           = new AssetResources(cpuBackend);
        /* if (isAsync) {
            // Bounded channel to enforce non-blocking backpressure via TryWrite
            var options = new BoundedChannelOptions(MaxSyncQueueCapacity) {
                SingleReader = true,
                SingleWriter = false,
                FullMode     = BoundedChannelFullMode.DropWrite // TryWrite fails fast when full
            };
            // eventChannel = Channel.CreateBounded<LoopEvent>(options);
            eventQueue      = null!;
            eventReady      = null!;        
        } else { */        
        queue               = new ConcurrentQueue<LoopEvent>();
        eventReady          = new AutoResetEvent(false);
        eventQueue          = new SmEventQueue(queue, eventReady);
        
        exitHandler         = ExitHandler;
        tuiShared           = new TuiSessionShared();
        guiShared           = new GuiSessionShared();
        sessionThreadId     = Environment.CurrentManagedThreadId;
        PosixSignalUtils.AddExitHandler(exitHandler);
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
        eventQueue.isDisposed = true;
        isDisposed = true;
        
        foreach (var session in sessions.Values) {
            session.client.Dispose();
        }

        // signal all loops to stop
        // eventChannel?.Writer.TryComplete();
        cts.Cancel();
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
    
    private TuiSession CreateTuiSession(SmClient client, Payload payload, bool isSync, out Memory<byte> firstPayload)
    {
        var firstLine       = payload.Span.IndexOf((byte)'\n');
        var args            = firstLine == -1 ? [] : GetArgs(payload.Span.Slice(0, firstLine));

        var loopTimer       = new LoopTimer(this, client, 60, isSync);
        var keys            = new SessionKeys(client.sessionId, default);
        var session         = new TuiSession(client, keys, this, loopTimer, TuiColorMode.RGB24);
        var viewSession     = new ViewSession(this, session, args, client, session.Backend, loopTimer);
        var guiView         = createGuiView(viewSession);
        session.viewSession = viewSession;
        
        session.guiView = guiView;
        AddSession(client.sessionId, session);
        
        var msgStart    = firstLine == -1 ? 0 : firstLine + 1;
        firstPayload    = payload.GetMemory(msgStart);
        
        SessionsChanged?.Invoke(new SessionsChangedArgs(client.sessionId, SessionsChangedType.Added));
        return session;
    }

    // protected virtual SmSession CreateGuiSession(SmClient client, bool isSync, out Memory<byte> firstPayload) => throw new NotImplementedException();
    private GuiSession CreateGuiSession(SmClient client, bool isSync, out Memory<byte> firstPayload)
    {
        var loopTimer       = new LoopTimer(this, client, 60, isSync);
        var sessionId       = client.sessionId;
        var sessionGuid     = UseSecureSessionGuids ? new SessionGuid(Guid.NewGuid()) : new SessionGuid(sessionId.id);
        var keys            = new SessionKeys(sessionId, sessionGuid);
        var session         = new GuiSession(client, keys, this, loopTimer);
        var viewSession     = new ViewSession(this, session, [], client, session.Backend, loopTimer);
        var guiView         = createGuiView(viewSession);
        session.viewSession = viewSession;
        
        session.guiView = guiView;
        AddSession(client.sessionId, session);
        
        firstPayload    = default;
        
        SessionsChanged?.Invoke(new SessionsChangedArgs(client.sessionId, SessionsChangedType.Added));
        return session;
    }

    /// Is called only from session loop thread. Synchronizing Add/Remove is not necessary for set dirty.
    private void AddSession(SessionId sessionId, SmSession session)
    {
        sessions          [sessionId] = session;
        concurrentSessions[sessionId] = session;
    }
    
    /// Is called only from session loop thread. Synchronizing Add/Remove is not necessary for set dirty.
    private void RemoveSession(SessionId sessionId)
    {
        concurrentSessions.TryRemove(sessionId, out _);
        if (!sessions.Remove(sessionId, out var session)) {
            return;
        }
        session.client.eventQueue = null;
        var batch = session.Batch;
        batch.Reset();
        resources.RemoveSessionResources(session.keys.guid);
        session.viewSession?.Reset();
        session.viewSession = null;
        
        SessionsChanged?.Invoke(new SessionsChangedArgs(sessionId, SessionsChangedType.Removed));
    }

    /// <summary>
    /// Enqueues a redraw (<see cref="IGuiView.RenderView"/>) for all clients of the app.
    /// </summary>
    /// <remarks>
    /// Applies to native GUI window, console TUI, browser clients, and terminal emulators.<br/>
    /// The method is threadsafe and can be called from any thread.
    /// </remarks>
    public void SetAppDirty()
    {
        if (sessionThreadId == Environment.CurrentManagedThreadId) {
            SetAppDirtySync();
            return;
        }
        eventQueue.TryEnqueueEvent(null, default, LoopEventType.SetAppDirty, null, default, 0);
    }
    
    private void SetAppDirtySync()
    {
        foreach (var session in sessions.Values) {
            if (session.SetDirty()) {
                continue;
            }
            session.client.TryEnqueueSessionEvent(LoopEventType.FrameTick, default);
        }
    }
    
    /// <summary>
    /// Enqueues a redraw (<see cref="IGuiView.RenderView"/>) for all clients of a specific session.
    /// </summary>
    /// <remarks>
    /// Applies to native GUI window, console TUI, browser clients, and terminal emulators.<br/>
    /// The method is threadsafe and can be called from any thread.
    /// </remarks>
    public void SetSessionDirty(SessionId sessionId)
    {
        if (!concurrentSessions.TryGetValue(sessionId, out var session)) {
            return;
        }
        if (session.SetDirty()) {
            return;
        }
        session.client.TryEnqueueSessionEvent(LoopEventType.FrameTick, default);
    }
    
    internal void SetSessionDirty(SmSession session)
    {
        if (session.SetDirty()) {
            return;
        }
        session.client.TryEnqueueSessionEvent(LoopEventType.FrameTick, default);
    }
}

