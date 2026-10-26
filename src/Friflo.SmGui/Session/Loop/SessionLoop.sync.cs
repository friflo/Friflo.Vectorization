// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading;

// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
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

            while (eventQueue.TryDequeue(out LoopEvent evt)) {
                // accumulate queued inputs per client - late-rendering
                var client = evt.Client;
                if (client != null) {
                    ProcessClientEventSync(client, evt.Type, evt.Payload);
                    continue;
                }
                ProcessEventSync(evt.Type, evt.Payload);
            }
        }
    }
    
    // process client event
    private void ProcessClientEventSync(SmClient client, LoopEventType type, Payload payload)
    {
        var isQueueEmpty    = client.DecrementPendingEvents();
        var sessionId       = client.sessionId;
        try {
            switch (type)
            {
                case LoopEventType.WebsocketConnected: {
                    CreateGuiSession(client, true, out var _);
                    // newSession.wsBatch.loopTimer!.InitTimer(CancellationToken.None);
                    break;
                }
                case LoopEventType.WebsocketInput:
                    if (sessions.TryGetValue(sessionId, out SmSession? session))
                    {
                        var payloadSpan = payload.Span;
                        session.ProcessInput(payloadSpan);
                        if (isQueueEmpty) {
                            var sendBuffer = session.IterateUI(resources);
                            if (!sendBuffer.IsEmpty) {
                                client.Send(sendBuffer);
                            }
                        }
                    }
                    break;
    
                case LoopEventType.TerminalConnected: {
                    var newSession      = CreateTuiSession(client, payload, true, out var initPayload);
                    var initialMessage  = newSession.StartSession();
                    
                    client.Send(new SmSendMessage(initialMessage));
                    
                    newSession.ProcessInput(initPayload.Span);
                    var sendBuffer = newSession.IterateUI(resources);
                    
                    client.Send(sendBuffer);
                    
                    // newSession.tuiBatch.loopTimer!.InitTimer(CancellationToken.None);
                    break;
                }
                case LoopEventType.TerminalDisconnected:
                case LoopEventType.WebsocketDisconnected:
                    RemoveSession(sessionId);
                    break;

                case LoopEventType.TerminalInput:
                    if (sessions.TryGetValue(sessionId, out session))
                    {
                        var payloadSpan = payload.Span;
                        session.ProcessInput(payloadSpan);
                        if (isQueueEmpty) {
                            var sendBuffer  = session.IterateUI(resources);
                            client.Send(sendBuffer);
                        }
                    }
                    break;
                case LoopEventType.FrameTick:
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
            payload.Return();
        }
    }
    
    // process event
    private void ProcessEventSync(LoopEventType type, Payload payload)
    {
        try {
            switch (type) {
                case LoopEventType.SetAppDirty:
                    SetAppDirtySync();
                    break;
            }
        } catch (Exception e) {
            Debug.Fail(e.ToString());
        } finally {
            payload.Return();
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
        while (eventQueue.TryDequeue(out LoopEvent evt))
        {
            // accumulate queued inputs per client - late-rendering
            var client = evt.Client;
            if (client != null) {
                ProcessClientEventSync(client, evt.Type, evt.Payload);
                if (--eventCount == 0) {
                    break;
                }
                continue;
            }
            ProcessEventSync(evt.Type, evt.Payload);
        }
    }
}