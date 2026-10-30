// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable RedundantLambdaParameterType
// ReSharper disable UseNullPropagation
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Session;


public sealed class LoopTimer : IDisposable
{
    private             SmEventQueue?     eventQueue;
    private             SmClient?         client;
    private             Timer?            syncTimer;
    private             PeriodicTimer?    asyncTimer;
    private             CancellationToken cancellationToken;
    private  readonly   bool              isSync;
    private             int               Period => 1000 / tickRate; // ms
    private             int               tickRate;                  // Hz
    private             bool              isInitialized;
    private  volatile   bool              isRunning;
    
    /// Creates a Noop LoopTimer
    internal LoopTimer(int tickRate, bool isSync)
    {
        this.tickRate = tickRate;
        this.isSync   = isSync;
    }

    internal LoopTimer(SmSessionLoop loop, SmClient client, int tickRate, bool isSync)
    {
        eventQueue    = loop.eventQueue;
        this.client   = client;
        this.tickRate = tickRate;
        this.isSync   = isSync;
    }

    public void Dispose()
    {
        isRunning = false;
        syncTimer?.Dispose();
        asyncTimer?.Dispose();
        syncTimer   = null;
        asyncTimer  = null;
        eventQueue  = null;
        client      = null;
    }
    
    /// <summary> Gets or sets a value indicating whether the session's loop ticker is active. </summary>
    /// <remarks>
    /// When set to <c>true</c>, continuous frame updates are triggered at the configured <see cref="TickRate"/>.
    /// Disabling this stops timer-driven ticks without preventing manual or event-driven redraws.
    /// </remarks>
    public bool Enabled
    {
        get => isRunning;
        set {
            if (value == isRunning || client == null) { 
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
    public int TickRate
    {
        get => tickRate;
        set {
            if (value == tickRate || client == null) { 
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
                EnqueueFrameTick();
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
    
    private void EnqueueFrameTick()
    {
        client?.TryEnqueueSessionEvent(LoopEventType.FrameTick, default);
    }

    private void StartFrameTickerSync()
    {
        syncTimer = new Timer(OnSyncTimer, this, dueTime: 0, period: Period);
    }

    private static void OnSyncTimer(object? s)
    {
        var timer = (LoopTimer)s!;
        if (timer.cancellationToken.IsCancellationRequested || !timer.isRunning) return;

        timer.EnqueueFrameTick();
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