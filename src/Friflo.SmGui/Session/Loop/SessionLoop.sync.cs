// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading;


// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Session;

//                                  --- sync session loop ---
public partial class SmSessionLoop
{
    // start
    public void RunSync()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        if (shardThread != null) {
            throw new InvalidOperationException("session loop is already running.");
        }
        
        // Block main caller thread directly or start dedicated loop thread
        RunSyncThreadLoop();
    }


    // run loop
    private void RunSyncThreadLoop()
    {
        try {
            RunEventLoopSync(cts.Token);
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

    // run event loop
    private void RunEventLoopSync(CancellationToken cancellationToken)
    {
        WaitHandle[] handles = [eventReady, cancellationToken.WaitHandle];

        while (!cancellationToken.IsCancellationRequested)
        {
            // Blocks thread cleanly with 0% CPU usage when empty
            // Wakes up immediately when an event arrives or cancellation is requested. ZERO allocations
            WaitHandle.WaitAny(handles);

            while (eventQueue.TryDequeue(out ClientEvent evt)) {
                // accumulate queued inputs per client - late-rendering
                var isQueueEmpty = evt.Client.DecrementPendingEvents();
                ProcessEventSync(evt, isQueueEmpty);
            }
        }
    }
    
    // process event
    private void ProcessEventSync(ClientEvent evt, bool isQueueEmpty)
    {
        var client      = evt.Client;
        var sessionId   = client.sessionId;
        try {
            switch (evt.Type)
            {
                case ClientEventType.WebsocketConnected: {
                    CreateGuiSession(client, true, out var _);
                    // newSession.wsBatch.loopTimer!.InitTimer(CancellationToken.None);
                    break;
                }
                case ClientEventType.WebsocketInput:
                    if (sessions.TryGetValue(sessionId, out SmSession? session))
                    {
                        var payload = evt.Payload.Span;
                        session.ProcessInput(payload);
                        if (isQueueEmpty) {
                            var sendBuffer = session.IterateUI(resources);
                            if (!sendBuffer.IsEmpty) {
                                client.Send(sendBuffer);
                            }
                        }
                    }
                    break;
    
                case ClientEventType.TerminalConnected: {
                    var newSession      = CreateTuiSession(evt, true, out var payload);
                    var initialMessage  = newSession.StartSession();
                    
                    client.Send(new SmSendMessage(initialMessage));
                    
                    newSession.ProcessInput(payload.Span);
                    var sendBuffer = newSession.IterateUI(resources);
                    
                    client.Send(sendBuffer);
                    
                    // newSession.tuiBatch.loopTimer!.InitTimer(CancellationToken.None);
                    break;
                }
                case ClientEventType.TerminalDisconnected:
                case ClientEventType.WebsocketDisconnected:
                    if (sessions.TryRemove(sessionId, out session)) {
                        DisposeSession(session);
                    }
                    break;

                case ClientEventType.TerminalInput:
                    if (sessions.TryGetValue(sessionId, out session))
                    {
                        var payload = evt.Payload.Span;
                        session.ProcessInput(payload);
                        if (isQueueEmpty) {
                            var sendBuffer  = session.IterateUI(resources);
                            client.Send(sendBuffer);
                        }
                    }
                    break;
                case ClientEventType.FrameTick:
                    if (sessions.TryGetValue(sessionId, out session) && isQueueEmpty)
                    {
                        var sendBuffer = session.IterateUI(resources);
                        client.Send(sendBuffer);
                    }
                    break;
            }
        } catch (Exception e) {
            Debug.Fail(e.ToString());
        } finally {
            evt.Payload.Return();
        }
    }
    
    /// <summary>
    /// Processes all currently pending events in the queue synchronously.<br/>
    /// Render all sessions with pending events and send rendered draw lists to session clients via stream / socket.
    /// </summary>
    public void IterateSessions()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        var eventCount = eventQueue.Count;
        while (eventQueue.TryDequeue(out ClientEvent evt))
        {
            // accumulate queued inputs per client - late-rendering
            var isQueueEmpty = evt.Client.DecrementPendingEvents();
            ProcessEventSync(evt, isQueueEmpty);
            if (--eventCount == 0) {
                break;
            }
        }
    }
}