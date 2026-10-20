// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable RedundantLambdaParameterType
// ReSharper disable UseNullPropagation
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;


internal sealed class LoopTimer : IDisposable
{
    private  readonly   TmSessionLoop     loop;
    private  readonly   TmClient          client;
    private             Timer?            syncTimer;
    private             PeriodicTimer?    asyncTimer;
    private             CancellationToken cancellationToken;
    private  readonly   bool              isSync;
    private             int               Period => 1000 / tickRate; // ms
    private             int               tickRate;                  // Hz
    private             bool              isInitialized;
    private  volatile   bool              isRunning;

    internal LoopTimer(TmSessionLoop loop, TmClient client, int tickRate, bool isSync)
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
    
    internal bool TickEnabled
    {
        get => isRunning;
        set {

            if (value == isRunning) { 
                return;
            }
            if (value) {
                Start();
            } else {
                Stop();
            }
        }
    }
    
    /// <summary> Gets or sets the execution frequency of the timer in Hertz (ticks per second). </summary>
    internal int TickRate
    {
        get => tickRate;
        set {
            if (value == tickRate) { 
                return;
            }
            tickRate = value;

            if (isSync) {
                if (isRunning) {
                    syncTimer?.Change(0, Period);
                }
            } else if (asyncTimer != null) {
                asyncTimer.Period = TimeSpan.FromMilliseconds(Period);
            }
        }
    }

    private void Stop()
    {
        if (!isRunning) {
            return;
        }
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

    private void Start()
    {
        if (isRunning) {
            return;
        }
        InitTimer(cancellationToken);
        isRunning = true;

        if (isSync) {
            // Reschedules the timer to tick immediately and then resume with framePeriod
            syncTimer?.Change(0, Period);
        } else {
            _ = StartFrameTickerAsync(cancellationToken);
        }
    }

    private void InitTimer(CancellationToken ct)
    {
        if (isInitialized) {
            return;
        }
        isInitialized       = true;
        cancellationToken   = ct;
        isRunning           = true;

        if (isSync) {
            StartFrameTickerSync();
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

    private void StartFrameTickerSync()
    {
        syncTimer = new Timer(OnSyncTimer, this, dueTime: 0, period: Period);
    }

    private static void OnSyncTimer(object? s)
    {
        var timer = (LoopTimer)s!;
        if (timer.cancellationToken.IsCancellationRequested || !timer.isRunning) return;

        timer.loop.TryEnqueueEvent(timer.client, ClientEventType.FrameTick, default);
    }
    
    static LoopTimer()
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