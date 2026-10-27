// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Session;

//                                  --- async session loop ---
public partial class SmSessionLoop
{
    // start
    public void RunAsync()
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
        var reader = eventChannel!.Reader;

        while (await reader.WaitToReadAsync(cancellationToken))
        {
            if (!reader.TryRead(out LoopEvent evt)) {
                continue;
            }
            if (evt.sessionId.id != 0) {
                await ProcessSessionEventAsync(evt);
            } else {
                await ProcessEventAsync (evt);
            }
        }
    }
    
    // process client event
    private async ValueTask ProcessSessionEventAsync (LoopEvent evt)
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
                case LoopEventType.TerminalDisconnected:
                    RemoveSession(evt.sessionId);
                    break;
                
                case LoopEventType.TerminalInput:
                    var payloadSpan = evt.Payload.Span;
                    session.ProcessInput(payloadSpan);
                    if (isQueueEmpty) {
                        var sendBuffer  = session.IterateUI(resources);
                        await client.SendAsync(sendBuffer, CancellationToken.None);
                    }
                    break;
                case LoopEventType.FrameTick:
                    if (isQueueEmpty)
                    {
                        var sendBuffer = session.IterateUI(resources);
                        await client.SendAsync(sendBuffer, CancellationToken.None);
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
    private async ValueTask ProcessEventAsync (LoopEvent evt)
    {
        try {
            switch (evt.type) {
                case LoopEventType.TerminalConnected: {
                    var client          = (SmClient)evt.state!;
                    var newSession      = CreateTuiSession(client, evt.Payload, false, out var initPayload);
                    var initialMessage  = newSession.StartSession();
                    
                    await client.SendAsync(new SmSendMessage(initialMessage), CancellationToken.None);
                    
                    newSession.ProcessInput(initPayload.Span);
                    var sendBuffer = newSession.IterateUI(resources);
                    
                    await client.SendAsync(sendBuffer, CancellationToken.None);
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
}