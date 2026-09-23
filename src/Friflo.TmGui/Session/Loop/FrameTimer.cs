// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable UseNullPropagation
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;


internal sealed class FrameTimer : IDisposable
{
    private  readonly   TmSessionLoop     loop;
    private  readonly   TmClient          client;
    private             Timer?            syncTimer;
    private             PeriodicTimer?    asyncTimer;
    private             CancellationToken cancellationToken;
    private  readonly   bool              isSync;
    private             int               Period => 1000 / tickRate; // ms
    internal            int               tickRate;                  // Hz
    internal volatile   bool              isRunning;

    internal FrameTimer(TmSessionLoop loop, TmClient client, int tickRate, bool isSync)
    {
        this.loop     = loop;
        this.client   = client;
        this.tickRate = tickRate;
        this.isSync   = isSync;
    }

    public void Dispose()
    {
        isRunning = false;
        syncTimer?.Dispose();
        asyncTimer?.Dispose();
    }

    internal void Stop()
    {
        if (!isRunning) return;
        isRunning = false;

        if (isSync) {
            // Disables future ticks for the OS/ThreadPool timer
            syncTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        } else {
            // Disposing the PeriodicTimer causes WaitForNextTickAsync to return false
            // and terminates the async loop cleanly without unnecessary CPU wakeups.
            asyncTimer?.Dispose();
            asyncTimer = null;
        }
    }

    internal void Restart()
    {
        if (isRunning) return;
        isRunning = true;

        if (isSync) {
            // Reschedules the timer to tick immediately and then resume with framePeriod
            syncTimer?.Change(0, Period);
        } else {
            _ = StartFrameTickerAsync(cancellationToken);
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

    internal void Start(CancellationToken ct)
    {
        cancellationToken = ct;
        isRunning = true;

        if (isSync) {
            StartFrameTickerSync(ct);
        } else {
            // Fire-and-forget background task for the async loop
            _ = StartFrameTickerAsync(ct);
        }
    }

    private async Task StartFrameTickerAsync(CancellationToken ct)
    {
        asyncTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(Period));
        try
        {
            while (await asyncTimer.WaitForNextTickAsync(ct))
            {
                loop.TryEnqueueEvent(client, ClientEventType.FrameTick, default);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected behavior on shutdown
        }
        catch (ObjectDisposedException)
        {
            // Expected when Stop() disposes the asyncTimer
        }
    }

    private void StartFrameTickerSync(CancellationToken ct)
    {
        syncTimer = new Timer(_ =>
        {
            if (ct.IsCancellationRequested || !isRunning) return;

            loop.TryEnqueueEvent(client, ClientEventType.FrameTick, default);
        }, null, dueTime: 0, period: Period);
    }
    
    static FrameTimer()
    {
        if (OperatingSystem.IsWindows()) {
            NativeTimer.timeBeginPeriod(1);
        }
    }
}


internal static class NativeTimer
{
    [DllImport("winmm.dll")]
    internal static extern uint timeBeginPeriod(uint uPeriod);
}