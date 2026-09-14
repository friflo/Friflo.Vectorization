// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading;
using Friflo.TmGui.TUI;
using Friflo.TmGui.TUI.VT100;


namespace Friflo.TmGui.Client;


public sealed partial class TmSessionLoop
{
    
    
    // -------------------------------------- similar sync / async code --------------------------------------
    // start
    public void StartSync()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        if (shardThread != null) {
            throw new InvalidOperationException("Engine is already running.");
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
        var reader = eventChannel.Reader;

        // Synchronous waiting via WaitToReadAsync + GetAwaiter().GetResult()
        // keeps execution tied strictly to this thread without ThreadPool switching
        while (reader.WaitToReadAsync(cancellationToken).AsTask().GetAwaiter().GetResult())
        {
            while (reader.TryRead(out ClientEvent evt))
            {
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
                    var payload         = evt.Payload;
                    var firstLine       = payload.Span.IndexOf((byte)'\n');
                    var client          = evt.Client;
                    var args            = firstLine == -1 ? [] : GetArgs(payload.Span.Slice(0, firstLine));
                    var connectInfo     = new ConnectInfo{ client = client, args = args };
                    var guiView         = createGuiView(connectInfo);
                    
                    var newSession      = new TuiSession(guiView, evt.Client, frameBuffer, TuiColorMode.RGB24);
                    sessions[client]    = newSession;
                    
                    var initialMessage  = newSession.StartSession();
                    client.Send(initialMessage);
                    
                    var rest            = firstLine == -1 ? payload.Span : payload.Span.Slice(firstLine + 1);
                    var sendBuffer      = newSession.ProcessInput(rest);
                    
                    client.Send(sendBuffer);
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
            }
        } catch (Exception e) {
            Debug.Fail(e.ToString());
        } finally {
            evt.Payload.Return();
        }
    }
}