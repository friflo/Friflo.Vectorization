// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using System.Threading;

// ReSharper disable RedundantLambdaParameterType
// ReSharper disable UseNullPropagation
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Session;


public sealed class SessionTimer : IDisposable
{
    private             SmClient?         client;
    private             Timer?            syncTimer;
    private             CancellationToken cancellationToken;
    private             int               Period => 1000 / tickRate; // ms
    private             int               tickRate;                  // Hz
    private             bool              isInitialized;
    private  volatile   bool              isRunning;
    
    /// Creates a Noop SessionTimer
    internal SessionTimer(int tickRate)
    {
        this.tickRate = tickRate;
    }

    internal SessionTimer(SmClient client, int tickRate)
    {
        this.client   = client;
        this.tickRate = tickRate;
    }

    public void Dispose()
    {
        isRunning = false;
        syncTimer?.Dispose();
        syncTimer   = null;

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

            if (isRunning) {
                syncTimer?.Change(0, Period);
            }
        }
    }

    private void Stop()
    {
        if (!isRunning) {
            return;
        }
        isRunning = false;

        // Disables future ticks for the OS/ThreadPool timer
        syncTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void Start()
    {
        if (isRunning) {
            return;
        }
        InitTimer(cancellationToken);
        isRunning = true;

        // Reschedules the timer to tick immediately and then resume with framePeriod
        syncTimer?.Change(0, Period);
    }

    private void InitTimer(CancellationToken ct)
    {
        if (isInitialized) {
            return;
        }
        isInitialized       = true;
        cancellationToken   = ct;
        isRunning           = true;

        syncTimer = new Timer(OnSyncTimer, this, dueTime: 0, period: Period);
    }

    private static void OnSyncTimer(object? s)
    {
        var timer = (SessionTimer)s!;
        if (timer.cancellationToken.IsCancellationRequested || !timer.isRunning) return;

        timer.client?.TryEnqueueSessionEvent(LoopEventType.FrameTick, default);
    }
    
    static SessionTimer()
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