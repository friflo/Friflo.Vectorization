// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

// ReSharper disable InlineTemporaryVariable
// ReSharper disable ReplaceWithFieldKeyword
// ReSharper disable RedundantLambdaParameterType
// ReSharper disable UseNullPropagation
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Session;

public sealed class SessionTimer
{
    private readonly    SessionId   sessionId;
    private readonly    AppTimer?   appTimer;
    private             bool        enabled;
    
    internal SessionTimer() { }

    internal SessionTimer(AppTimer appTimer, SessionId sessionId)
    {
        this.sessionId  = sessionId;
        this.appTimer   = appTimer;
    }
    
    /// <summary> Gets or sets a value indicating whether the session's loop ticker is active. </summary>
    /// <remarks>
    /// When set to <c>true</c>, continuous frame updates are triggered at the configured <see cref="SmSessionLoop.TickRate"/>.
    /// Disabling this stops timer-driven ticks without preventing manual or event-driven redraws.
    /// </remarks>
    public bool Enabled
    {
        get => enabled;
        set {
            if (value == enabled) { 
                return;
            }
            enabled = value;
            appTimer?.AddRemoveSession(sessionId, value);
        }
    }
}

internal sealed class AppTimer : IDisposable
{
    private  readonly   List<SessionId>     tickedSessions = [];
    private  readonly   SmEventQueue        eventQueue;
    private             Timer?              syncTimer;
    private             CancellationToken   cancellationToken;
    private             int                 Period => 1000 / tickRate; // ms
    private             int                 tickRate;                  // Hz
    private             bool                isInitialized;
    private  volatile   bool                isRunning;
    
    internal AppTimer(SmEventQueue eventQueue, int tickRate)
    {
        this.eventQueue = eventQueue;
        this.tickRate   = tickRate;
    }

    public void Dispose()
    {
        isRunning = false;
        syncTimer?.Dispose();
        syncTimer   = null;
    }
    
    internal void AddRemoveSession(SessionId sessionId, bool add)
    {
        var sessions    = tickedSessions;
        var index       = sessions.IndexOf(sessionId);
        if (add) {
            if (index != -1) return;
            sessions.Add(sessionId);
            if (sessions.Count == 1) {
                Start();
            }
            return;
        }
        if (index == -1) return;
        sessions.RemoveAt(index);
        if (sessions.Count == 0) {
            Stop();
        }
    }

    internal int TickRate
    {
        get => tickRate;
        set {
            if (value == tickRate) { 
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
        var timer = (AppTimer)s!;
        if (timer.cancellationToken.IsCancellationRequested || !timer.isRunning) return;

        timer.eventQueue.TryEnqueueEvent(null, default, LoopEventType.AppTick, null, default, 0);
    }
    
    internal void TickSessions(SmSessionLoop loop)
    {
        var queue       = eventQueue;
        var sessions    = tickedSessions;
        var count       = sessions.Count;
        for (var n = 0; n < count; n++)
        {
            var sessionId   = sessions[n];
            var session     = loop.GetSession(sessionId);
            if (session == null) {
                continue;
            }
            queue.TryEnqueueEvent(session.client, sessionId, LoopEventType.FrameTick, null, default, 0);
        }
    }
    
    static AppTimer()
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