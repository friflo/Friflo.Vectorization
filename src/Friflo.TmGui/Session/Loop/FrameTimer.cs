// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable UseNullPropagation
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;


internal sealed class FrameTimer : IDisposable
{
    private  readonly   TmSessionLoop   loop;
    private  readonly   TmClient        client;
    private             Timer?          syncTimer;
    private             PeriodicTimer?  asyncTimer;
    private  readonly   bool            isSync;
    private             int             Period  => 1000 / tickRate; // ms
    internal            int             tickRate;                   // Hz
    internal volatile   bool            isRunning;

    internal FrameTimer(TmSessionLoop loop, TmClient client, int tickRate, bool isSync)
    {
        this.loop       = loop;
        this.client     = client;
        this.tickRate   = tickRate;
        this.isSync     = isSync;
    }

    public void Dispose()
    {
        isRunning = false;
        syncTimer?.Dispose();
        asyncTimer?.Dispose();
    }

    internal void Stop()
    {
        isRunning = false;

        if (isSync) {
            // Disables future ticks for the OS/ThreadPool timer
            syncTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    internal void Restart()
    {
        isRunning = true;

        if (isSync) {
            // Reschedules the timer to tick immediately and then resume with framePeriod
            syncTimer?.Change(0, Period);
        }
    }

    internal void SetTickRate(int rate)
    {
        tickRate = rate;
        
        if (isSync) {
            if (isRunning) {
                syncTimer?.Change(0, Period);
            }
        } else if (asyncTimer != null) {
            asyncTimer.Period = TimeSpan.FromMilliseconds(Period);
        }
    }
    
    internal void Start(CancellationToken cancellationToken)
    {
        isRunning = true;

        if (isSync) {
            StartFrameTickerSync(cancellationToken);
        } else  {
            // Fire-and-forget background task for the async loop
            _ = StartFrameTickerAsync(cancellationToken);
        }
    }

    private async Task StartFrameTickerAsync(CancellationToken cancellationToken)
    {
        asyncTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(Period));
        try
        {
            while (await asyncTimer.WaitForNextTickAsync(cancellationToken))
            {
                if (isRunning) {
                    // Clean, non-blocking, and zero-allocation
                    loop.TryEnqueueEvent(client, ClientEventType.FrameTick, default);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected behavior on shutdown
        }
    }

    private void StartFrameTickerSync(CancellationToken cancellationToken)
    {
        syncTimer = new Timer(_ =>
        {
            if (cancellationToken.IsCancellationRequested || !isRunning) return;

            loop.TryEnqueueEvent(client, ClientEventType.FrameTick, default);
        }, null, dueTime: 0, period: Period);
    }
}