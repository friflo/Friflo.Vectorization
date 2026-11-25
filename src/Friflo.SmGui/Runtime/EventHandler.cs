// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Threading;
using Friflo.SmGui.Session;

// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Runtime;


internal delegate void ProcessReadyEvents();


/// <summary>
/// Thread-safe, zero-allocation wake-up coordinator between background producers and the UI thread.<br/>
/// Uses <see cref="NotifyEventReady"/> to signal pending work and dispatches <see cref="ProcessReadyEvents"/> on the UI thread.
/// </summary>
/// <remarks>
/// Subclasses of this class can reduce latency between remote browser client input (such as mouse movement)
/// and rendering when using <see cref="SmSessionLoop.IterateSessions"/>.<br/>
/// <br/>
/// <see cref="SmSessionLoop.IterateSessions"/> is typically called inside <c>Update()</c> or <c>Draw()</c>
/// of a graphics backend such as MonoGame, KNI, raylib or OpenTK.<br/>
/// At 60 Hz, polling once per frame can introduce up to ~16 ms of latency between incoming input and rendering.<br/>
/// If a backend exposes access to its event or message loop, a custom implementation can wake and process
/// events with lower latency - for example with SDL3 or SDL. See <c>Sdl3RuntimeEventHandler</c>.
/// <example>
/// Using an <see cref="SmRuntimeEventHandler"/> allows processing incoming remote messages during the idle phase.
/// Without it, message processing occurs when calling <see cref="SmSessionLoop.IterateSessions"/>.
/// <code>
/// |2 ms|                           14 ms                         | 16 ms
/// |****|.........................................................|
/// legend: **** busy with rendering
///         .... idle
/// </code>
/// </example>
/// </remarks>
public abstract class SmRuntimeEventHandler
{
    internal event  ProcessReadyEvents? ProcessReadyEvents;
    private         int                 eventReadyPending;
    
    /// <summary>
    /// Notify that events need to be processed on the UI thread within the main message loop.<br/>
    /// It is currently used to process events at <see cref="SmSessionLoop.IterateSessions"/>. <br/>
    /// </summary>
    /// <remarks>
    /// Method is thread-safe - can be called from any thread.
    /// </remarks>
    internal void NotifyEventReady()
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
