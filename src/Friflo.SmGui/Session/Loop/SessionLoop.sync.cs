// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading;

// ReSharper disable SuggestVarOrType_SimpleTypes
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
                if (evt.sessionId.id != 0) {
                    ProcessSessionEventSync(evt);
                } else {
                    ProcessEventSync (evt);
                }
            }
        }
    }
    
    // process client event
    private void ProcessSessionEventSync (in LoopEvent evt)
    {
        if (!sessions.TryGetValue(evt.sessionId, out var session)) {
            return;
        }
        var client          = session.client;
        // accumulate queued inputs per client - late-rendering
        var isQueueEmpty    = client.DecrementPendingEvents();
        
        try {
            switch (evt.type)
            {
                case LoopEventType.WebsocketInput:
                    session.ProcessInput(evt.Payload.Span);
                    if (isQueueEmpty) {
                        var sendBuffer = session.IterateUI(resources);
                        if (!sendBuffer.IsEmpty) {
                            client.Send(sendBuffer);
                        }
                    }
                    break;
    
                case LoopEventType.TerminalDisconnected:
                case LoopEventType.WebsocketDisconnected:
                    RemoveSession(evt.sessionId);
                    break;

                case LoopEventType.TerminalInput:
                    var payloadSpan = evt.Payload.Span;
                    session.ProcessInput(payloadSpan);
                    if (isQueueEmpty) {
                        var sendBuffer  = session.IterateUI(resources);
                        client.Send(sendBuffer);
                    }
                    break;
                case LoopEventType.FrameTick:
                    if (isQueueEmpty)
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
    
    // process event
    private void ProcessEventSync (in LoopEvent evt)
    {
        try {
            switch (evt.type) {
                case LoopEventType.WebsocketConnected: {
                    var client = (SmClient)evt.state!;
                    CreateGuiSession(client, true, out var _);
                    break;
                }
                case LoopEventType.TerminalConnected: {
                    var client          = (SmClient)evt.state!;
                    var newSession      = CreateTuiSession(client, evt.Payload, true, out var initPayload);
                    var initialMessage  = newSession.StartSession();
                    
                    client.Send(new SmSendMessage(initialMessage));
                    
                    newSession.ProcessInput(initPayload.Span);
                    var sendBuffer = newSession.IterateUI(resources);
                    
                    client.Send(sendBuffer);
                    break;
                }
                case LoopEventType.SetAppDirty:
                    SetAppDirtySync();
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
        while (eventQueue.TryDequeue(out LoopEvent evt))
        {
            if (evt.sessionId.id != 0) {
                ProcessSessionEventSync(evt);
            } else {
                ProcessEventSync(evt);
            }
            if (--eventCount == 0) {
                break;
            }
        }
    }
}