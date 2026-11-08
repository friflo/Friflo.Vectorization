// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable InlineTemporaryVariable
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Session;


public partial class SmSessionLoop
{
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
                    ProcessAppEvent (evt);
                }
            }
        }
    }
    
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
                case LoopEventType.RenderView:
                    if (isQueueEmpty) {
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
    
    private void ProcessAppEvent (in LoopEvent evt)
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
                    var newSession      = CreateTuiSession(client, evt.Payload, out var initPayload);
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
                case LoopEventType.AppTick:
                    TickSessions();
                    break;
            }
        } catch (Exception e) {
            Debug.Fail(e.ToString());
        } finally {
            evt.Payload.Return();
        }
    }
    
    private void TickSessions()
    {
        var tickedSessions = appTimer.tickedSessions;
        var count = tickedSessions.Count;
        for (var n = 0; n < count; n++)
        {
            var sessionId = tickedSessions[n];
            if (!sessions.TryGetValue(sessionId, out var session)) {
                continue;
            }
            if (!session.client.IsQueueEmpty) {
                continue;
            }
            try {
                var sendBuffer = session.ProcessUI(resources);
                session.client.Send(sendBuffer);  // MUST not block. No backpressure
            }
            catch (Exception e) {
                Debug.Fail(e.ToString());
            }
        }
    }
    
    /// <summary>
    /// Processes all currently pending events in the queue synchronously.<br/>
    /// Render all sessions with pending events and send rendered draw lists to session clients via stream / socket.
    /// </summary>
    public void IterateSessions()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        var localQueue  = queue;
        var eventCount  = localQueue.Count;
        
        while (localQueue.TryDequeue(out LoopEvent evt))
        {
            if (evt.sessionId.id != 0) {
                ProcessSessionEvent(evt);
            } else {
                ProcessAppEvent(evt);
            }
            if (--eventCount == 0) {
                break;
            }
        }
    }
    
    /// <summary>
    /// Processes all currently pending events in the queue synchronously.<br/>
    /// Render all sessions with pending events and send rendered draw lists to session clients via stream / socket.
    /// </summary>
    public async Task IterateSessionsAsync()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        var localQueue = wasmQueue;

        while (!isDisposed)
        {
            while (localQueue.Count > 0)
            {
                var evt = localQueue.Dequeue();
                
                if (evt.sessionId.id != 0) {
                    ProcessSessionEvent(evt);
                } else {
                    ProcessAppEvent(evt);
                }
            }

            await wasmEventReady.WaitAsync();
        }
    }
}