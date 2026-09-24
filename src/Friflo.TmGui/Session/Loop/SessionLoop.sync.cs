// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading;
using Friflo.TmGui.TUI.VT100;

// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;

//                                  --- sync session loop ---
public partial class TmSessionLoop
{
    // start
    public void StartSync()
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
            // Wakes up immediately when an event arrives or cancellation is requested
            // ZERO allocations
            WaitHandle.WaitAny(handles);

            while (eventQueue.TryDequeue(out ClientEvent evt)) {
                ProcessEventSync(evt);
            }
        }
    }
    
    // process event
    private void ProcessEventSync(ClientEvent evt)
    {
        try {
            switch (evt.Type)
            {
                case ClientEventType.TerminalConnected: {
                    var newSession      = CreateSession(evt, true, out var payload);
                    var initialMessage  = newSession.StartSession();
                    
                    evt.Client.Send(initialMessage);
                    
                    var sendBuffer      = newSession.ProcessInput(payload.Span);
                    
                    evt.Client.Send(sendBuffer);
                    
                    newSession.tuiBatch.frameTimer!.Start(CancellationToken.None);
                    break;
                }
                case ClientEventType.TerminalDisconnected:
                    sessions.Remove(evt.Client);
                    break;

                case ClientEventType.TerminalInput:
                    if (sessions.TryGetValue(evt.Client, out TuiSession? session))
                    {
                        var payload     = evt.Payload.Span;
                        var sendBuffer  = session.ProcessInput(payload);
                        evt.Client.Send(sendBuffer);
                    }
                    break;
                case ClientEventType.FrameTick:
                    if (sessions.TryGetValue(evt.Client, out session))
                    {
                        var sendBuffer = session.ProcessInput(default);
                        evt.Client.Send(sendBuffer);
                    }
                    break;
            }
        } catch (Exception e) {
            Debug.Fail(e.ToString());
        } finally {
            evt.Payload.Return();
        }
    }
}