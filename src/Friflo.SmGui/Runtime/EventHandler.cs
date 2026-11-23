// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Threading;

// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Runtime;


public delegate void ProcessReadyEvents();


/// <summary>
/// Thread-safe, zero-allocation wake-up coordinator between background producers and the UI thread.<br/>
/// Uses <see cref="NotifyEventReady"/> to signal pending work and dispatch <see cref="ProcessReadyEvents"/> on the UI thread.
/// </summary>
public abstract class SmRuntimeEventHandler
{
    public  event   ProcessReadyEvents? ProcessReadyEvents;
    private         int                 eventReadyPending;
    
    /// <summary>
    /// Notify that events need to be processed on the UI thread within the main message loop.<br/>
    /// It is currently used to process events at <see cref="Friflo.SmGui.Session.SmSessionLoop.IterateSessions"/>. <br/>
    /// </summary>
    /// <remarks>
    /// Method is thread-safe - can be called from any thread.
    /// </remarks>
    public void NotifyEventReady()
    {
        if (Interlocked.Exchange(ref eventReadyPending, 1) == 0) {
            OnNotifyEventReady(); // Wake up main thread to run:  ProcessEvents() > IterateSessions()
        }
    }
    
    protected abstract void OnNotifyEventReady();
    
    public void ProcessEvents()
    {
        Interlocked.Exchange(ref eventReadyPending, 0);
        ProcessReadyEvents?.Invoke();
    }
}
