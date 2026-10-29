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
    public void Run()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        WaitHandle[] handles = [eventReady, cts.Token.WaitHandle];

        while (!cts.Token.IsCancellationRequested)
        {
            // Blocks thread cleanly with 0% CPU usage when empty
            // Wakes up immediately when an event arrives or cancellation is requested. ZERO allocations
            WaitHandle.WaitAny(handles);

            while (queue.TryDequeue(out LoopEvent evt)) {
                if (evt.sessionId.id != 0) {
                    ProcessSessionEvent(evt);
                } else {
                    ProcessEvent (evt);
                }
            }
        }
    }
    
    // process client event
    private void ProcessSessionEvent (in LoopEvent evt)
    {
        if (!sessions.TryGetValue(evt.sessionId, out var session)) {
            evt.Payload.Return();
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
                        var sendBuffer = session.ProcessUI(resources);
                        if (!sendBuffer.IsEmpty) {
                            client.Send(sendBuffer);  // MUST not block. No backpressure
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
                        var sendBuffer  = session.ProcessUI(resources);
                        client.Send(sendBuffer);  // MUST not block. No backpressure
                    }
                    break;
                case LoopEventType.FrameTick:
                    if (isQueueEmpty)
                    {
                        var sendBuffer = session.ProcessUI(resources);
                        client.Send(sendBuffer);  // MUST not block. No backpressure
                    }
                    break;
                case LoopEventType.PostMessage:
                    session.guiView!.OnMessage(evt.state, evt.Payload, evt.data);
                    break;
            }
        } catch (Exception e) {
            Debug.Fail(e.ToString());
        } finally {
            evt.Payload.Return();
        }
    }
    
    // process event
    private void ProcessEvent (in LoopEvent evt)
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
                    
                    client.Send(new SmSendMessage(initialMessage));  // MUST not block. No backpressure
                    
                    newSession.ProcessInput(initPayload.Span);
                    var sendBuffer = newSession.ProcessUI(resources);
                    
                    client.Send(sendBuffer);  // MUST not block. No backpressure
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
        var eventCount = queue.Count;
        while (queue.TryDequeue(out LoopEvent evt))
        {
            if (evt.sessionId.id != 0) {
                ProcessSessionEvent(evt);
            } else {
                ProcessEvent(evt);
            }
            if (--eventCount == 0) {
                break;
            }
        }
    }
}