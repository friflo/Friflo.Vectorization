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


public readonly struct SessionResources
{
    private  readonly   TmGuiBackend                rootBackend;
    private  readonly   Dictionary<object, int>     texture2Id      = new();
    public   readonly   Dictionary<string, WsImage> stringToImage   = new();
    public   readonly   List<WsImage>               images          = [default];
    
    private int AddTexture(TmTexture texture)
    {
        var textureId = images.Count;
        texture2Id.Add(texture.native!, textureId);
        var asset = rootBackend.GetTextureImage(texture);
        var image = new WsImage { textureId = textureId, asset = asset, texture = texture };
        images.Add(image);
        if (asset.name != null) {
            stringToImage.TryAdd(asset.name, image);   // TODO fix me!!!
        }
        return textureId;
    }
    
    public SessionResources(TmGuiBackend rootBackend)
    {
        this.rootBackend = rootBackend;
    }

    public int GetTexture(TmTexture texture)
    {
        if (texture2Id.TryGetValue(texture.native!, out int id)) {
            return id;
        }
        return AddTexture(texture);
    }
}

public struct WsImage   // TODO  rename
{
    public  int             textureId;
    public  TmImageAsset    asset;
    public  TmTexture       texture;

    public override string ToString() => $"{asset.name} - {texture}";
}

public sealed partial class TmSessionLoop : IDisposable
{
    private  readonly   bool                                isAsync;
    private  readonly   ConcurrentQueue<ClientEvent>        eventQueue;     // used by: sync loop
    private  readonly   AutoResetEvent                      eventReady;     // used by: sync loop
    private  readonly   Channel<ClientEvent>                eventChannel;   // used by: async loop
    private  readonly   Dictionary<TmClient, TmSession>     sessions;       // Raw non-thread-safe state (accessed exclusively by _shardThread)
    private  readonly   FrameBuffer                         frameBuffer;    // shared among all sessions - is accessed single threaded
    private  readonly   SixelDrawer                         sixelDrawer;    // shared among all sessions
    private  readonly   CreateGuiView                       createGuiView;  // IBatchRenderer factory
    private  readonly   CreateSession                       createSession;
    public   readonly   TmGuiBackend                        rootBackend;    // shared among all sessions
    private  readonly   CancellationTokenSource             cts = new();
    private             Thread?                             shardThread;
    private             bool                                isDisposed;
    private  readonly   Action                              exitHandler;
    public   readonly   SessionResources                    resources;

    private const int MaxSyncQueueCapacity = 32;
    
    public TmSessionLoop(bool isAsync, TmGuiBackend backend, CreateGuiView createGuiView, CreateSession createSession)
    {
        this.isAsync        = isAsync;
        this.createGuiView  = createGuiView;
        this.createSession  = createSession;
        this.rootBackend    = backend; 
        resources           = new SessionResources(backend);
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
        frameBuffer         = new FrameBuffer();
        sixelDrawer         = new SixelDrawer();
        exitHandler         = ExitHandler;
        PosixSignalUtils.AddExitHandler(exitHandler);
    }
    
    public async ValueTask EnqueueEventAsync(TmClient client, ClientEventType type, Payload payload)
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
    
    private TuiSession CreateTuiSession(ClientEvent evt, bool isSync, out Memory<byte> firstPayload)
    {
        var payload     = evt.Payload;
        var firstLine   = payload.Span.IndexOf((byte)'\n');
        var client      = evt.Client;
        var args        = firstLine == -1 ? [] : GetArgs(payload.Span.Slice(0, firstLine));

        var frameTimer  = new FrameTimer(this, evt.Client, 60, isSync);
        var session     = new TuiSession(evt.Client, frameBuffer, frameTimer, sixelDrawer, rootBackend.Assets, TuiColorMode.RGB24);

        var sessionInfo = new SessionInfo{ client = client, backend = session.Backend, args = args };
        var guiView     = createGuiView(sessionInfo);
        
        session.guiView = guiView;
        sessions[client]= session;
        var msgStart    = firstLine == -1 ? 0 : firstLine + 1;
        firstPayload    = payload.GetMemory(msgStart);
        return session;
    }

    // protected virtual TmSession CreateGuiSession(TmClient client, bool isSync, out Memory<byte> firstPayload) => throw new NotImplementedException();
    private TmSession CreateGuiSession(TmClient client, bool isSync, out Memory<byte> firstPayload)
    {
        TmSession session = createSession(client, rootBackend);
        
        var sessionInfo = new SessionInfo{ client = client, backend = session.Backend, args = [] };
        var guiView     = createGuiView(sessionInfo);
        
        session.guiView = guiView;
        sessions[client]= session;
        firstPayload    = default;
        return session;
    }
}

public delegate TmSession CreateSession(TmClient client, TmGuiBackend backend);
