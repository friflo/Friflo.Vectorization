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
    private readonly    TmSessionLoop   loop;
    private readonly    TmClient        client;
    private             Timer?          syncTimer;
    private             PeriodicTimer?  asyncTimer;
    private             int             framePeriod; // ms
    private readonly    bool            isSync;
    private volatile    bool            isPaused;

    internal FrameTimer(TmSessionLoop loop, TmClient client, int framePeriod, bool isSync)
    {
        this.loop           = loop;
        this.client         = client;
        this.framePeriod    = framePeriod;
        this.isSync         = isSync;
    }

    public void Dispose()
    {
        isPaused = true;
        syncTimer?.Dispose();
        asyncTimer?.Dispose();
    }

    internal void Stop()
    {
        isPaused = true;

        if (isSync) {
            // Disables future ticks for the OS/ThreadPool timer
            syncTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    internal void Restart()
    {
        isPaused = false;

        if (isSync) {
            // Reschedules the timer to tick immediately and then resume with framePeriod
            syncTimer?.Change(0, framePeriod);
        }
    }

    internal void SetPeriod(int period)
    {
        framePeriod = period;

        if (isSync) {
            if (!isPaused) {
                syncTimer?.Change(0, period);
            }
        } else if (asyncTimer != null) {
            asyncTimer.Period = TimeSpan.FromMilliseconds(period);
        }
    }
    
    internal void Start(CancellationToken cancellationToken)
    {
        isPaused = false;

        if (isSync) {
            StartFrameTickerSync(cancellationToken);
        } else  {
            // Fire-and-forget background task for the async loop
            _ = StartFrameTickerAsync(cancellationToken);
        }
    }

    private async Task StartFrameTickerAsync(CancellationToken cancellationToken)
    {
        asyncTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(framePeriod));
        try
        {
            while (await asyncTimer.WaitForNextTickAsync(cancellationToken))
            {
                if (!isPaused) {
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
            if (cancellationToken.IsCancellationRequested || isPaused) return;

            loop.TryEnqueueEvent(client, ClientEventType.FrameTick, default);
        }, null, dueTime: 0, period: framePeriod);
    }
}