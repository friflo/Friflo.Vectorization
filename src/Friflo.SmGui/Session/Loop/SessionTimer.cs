// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

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
    /// When set to <c>true</c>, continuous frame updates are triggered at the configured <see cref="AppTimer.TickRate"/> at <see cref="SmSessionLoop.AppTimer"/>.<br/>
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


public sealed class AppTimer : IDisposable
{
    internal readonly   List<SessionId>     tickedSessions = [];
    private  readonly   SmEventQueue        eventQueue;
    private             SmTimer?            syncTimer;
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
        syncTimer?.OnTick -= OnSyncTimer;
        syncTimer?.Dispose();
        syncTimer   = null;
    }
    
    /// <summary> Gets or sets the execution frequency of the timer in Hertz (ticks per second). </summary>
    public int TickRate
    {
        get => tickRate;
        set {
            if (value == tickRate) { 
                return;
            }
            tickRate = value;

            if (isRunning) {
                syncTimer?.SetPeriod(Period);
            }
        }
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

    private void Stop()
    {
        if (!isRunning) {
            return;
        }
        isRunning = false;
        
        syncTimer?.Stop();
    }

    private void Start()
    {
        if (isRunning) {
            return;
        }
        InitTimer();
        isRunning = true;

        syncTimer?.Start();
    }

    private void InitTimer()
    {
        if (isInitialized) {
            return;
        }
        isInitialized   = true;
        isRunning       = true;

        syncTimer = new SyncTimer(Period);
        syncTimer.OnTick += OnSyncTimer;
    }

    private void OnSyncTimer()
    {
        if (!isRunning) {
            return;
        }
        eventQueue.TryEnqueueEvent(null, default, LoopEventType.AppTick, null, default, 0);
    }
}
