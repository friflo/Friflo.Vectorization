// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Friflo.TmGui.TUI;
using Friflo.TmGui.TUI.VT100;


namespace Friflo.TmGui.Client;

//                                  --- async session loop ---
public partial class TmSessionLoop
{
    // start
    public void StartAsync()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        if (shardThread != null)        {
            throw new InvalidOperationException("Engine is already running.");
        }
        shardThread = new Thread(RunAsyncThreadLoop) {
            IsBackground = true,
            Name = "ShardLoopThread"
        };
        shardThread.Start();
    }


    // run loop
    private void RunAsyncThreadLoop()
    {
        try {
            RunEventLoopAsync(cts.Token).GetAwaiter().GetResult();
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
    private async Task RunEventLoopAsync(CancellationToken cancellationToken)
    {
        var reader = eventChannel.Reader;



        while (await reader.WaitToReadAsync(cancellationToken))
        {
            while (reader.TryRead(out ClientEvent evt))
            {
                await ProcessEventAsync(evt);
            }
        }
    }
    
    // process event
    private async ValueTask ProcessEventAsync(ClientEvent evt)
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
                    
                    var initialMessage = newSession.StartSession();
                    await client.SendAsync(initialMessage, CancellationToken.None);
                    
                    var rest            = firstLine == -1 ? payload.Span : payload.Span.Slice(firstLine + 1);
                    var sendBuffer  = newSession.ProcessInput(rest);
                    
                    await client.SendAsync(sendBuffer, CancellationToken.None);
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
                        await evt.Client.SendAsync(sendBuffer, CancellationToken.None);
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