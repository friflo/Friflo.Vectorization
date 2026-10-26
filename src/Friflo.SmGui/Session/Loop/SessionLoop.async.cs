// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable SuggestVarOrType_SimpleTypes
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
            if (!reader.TryRead(out ClientEvent evt)) {
                continue;
            }
            // accumulate queued inputs per client - late-rendering
            var client = evt.Client;
            if (client != null) {
                await ProcessClientEventAsync(client, evt.Type, evt.Payload);
                continue;
            }
            ProcessEventAsync(evt.Type, evt.Payload);
        }
    }
    
    // process client event
    private async ValueTask ProcessClientEventAsync(SmClient client, ClientEventType type, Payload payload)
    {
        var isQueueEmpty    = client.DecrementPendingEvents();
        var sessionId       = client.sessionId;
        try {
            switch (type)
            {
                case ClientEventType.TerminalConnected: {
                    var newSession      = CreateTuiSession(client, payload, false, out var initPayload);
                    var initialMessage  = newSession.StartSession();
                    
                    await client.SendAsync(new SmSendMessage(initialMessage), CancellationToken.None);
                    
                    newSession.ProcessInput(initPayload.Span);
                    var sendBuffer = newSession.IterateUI(resources);
                    
                    await client.SendAsync(sendBuffer, CancellationToken.None);
                    
                    // newSession.tuiBatch.loopTimer!.InitTimer(CancellationToken.None);
                    break;
                }
                case ClientEventType.TerminalDisconnected:
                    RemoveSession(sessionId);
                    break;

                case ClientEventType.TerminalInput:
                    if (sessions.TryGetValue(sessionId, out var session))
                    {
                        var payloadSpan = payload.Span;
                        session.ProcessInput(payloadSpan);
                        if (isQueueEmpty) {
                            var sendBuffer  = session.IterateUI(resources);
                            await client.SendAsync(sendBuffer, CancellationToken.None);
                        }
                    }
                    break;
                case ClientEventType.FrameTick:
                    if (sessions.TryGetValue(sessionId, out session) && isQueueEmpty)
                    {
                        var sendBuffer = session.IterateUI(resources);
                        await client.SendAsync(sendBuffer, CancellationToken.None);
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
    private void ProcessEventAsync(ClientEventType type, Payload payload)
    {
        try {
            switch (type) {
                case ClientEventType.SetAppDirty:
                    SetAppDirtySync();
                    break;
            }
        } catch (Exception e) {
            Debug.Fail(e.ToString());
        } finally {
            payload.Return();
        }
    }
}