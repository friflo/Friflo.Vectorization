// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Friflo.TmGui.TUI.VT100;

// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;

//                                  --- async session loop ---
public partial class TmSessionLoop
{
    // start
    public void StartAsync()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        if (shardThread != null)        {
            throw new InvalidOperationException("session loop is already running.");
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
            if (!reader.TryRead(out ClientEvent evt)) {
                continue;
            }
            // Try reading the next event to check if currentEvt is the last in the batch
            while (reader.TryRead(out ClientEvent nextEvt))
            {
                // accumulate queued inputs - late-rendering
                await ProcessEventAsync(evt, isQueueEmpty: false);
                evt = nextEvt;
            }
            await ProcessEventAsync(evt, isQueueEmpty: true);
        }
    }
    
    // process event
    private async ValueTask ProcessEventAsync(ClientEvent evt, bool isQueueEmpty)
    {
        try {
            switch (evt.Type)
            {
                case ClientEventType.TerminalConnected: {
                    var newSession      = CreateSession(evt, false, out var payload);
                    var initialMessage  = newSession.StartSession();
                    
                    await evt.Client.SendAsync(initialMessage, CancellationToken.None);
                    
                    newSession.ProcessInput(payload.Span);
                    var sendBuffer = newSession.IterateTui();
                    
                    await evt.Client.SendAsync(sendBuffer, CancellationToken.None);
                    
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
                        session.ProcessInput(payload);
                        if (isQueueEmpty) {
                            var sendBuffer  = session.IterateTui();
                            await evt.Client.SendAsync(sendBuffer, CancellationToken.None);
                        }
                    }
                    break;
                case ClientEventType.FrameTick:
                    if (sessions.TryGetValue(evt.Client, out session))
                    {
                        var sendBuffer = session.IterateTui();
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