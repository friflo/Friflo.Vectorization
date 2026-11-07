// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using System.Threading;

// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Session;


public abstract class SmTimer : IDisposable
{
    public  event   Action? OnTick;
    
    public  void Start()                => StartTimer();
    public  void Stop()                 => StopTimer();
    public  void SetPeriod(int period)  => SetTimerPeriod(period);
    
    protected   abstract    void StartTimer();
    protected   abstract    void StopTimer();
    protected   abstract    void SetTimerPeriod(int period);
    public      abstract    void Dispose();
    
    protected void RaiseTick()  => OnTick?.Invoke();
}



internal sealed class SyncTimer : SmTimer
{
    private readonly    Timer               syncTimer;
    private readonly    CancellationToken   cancellationToken = CancellationToken.None;
    private             int                 period;
    
    internal SyncTimer(int period)
    {
        this.period = period;
        syncTimer   = new Timer(OnSyncTimer, this, dueTime: Timeout.Infinite, period: Timeout.Infinite);
    }
    
    private static void OnSyncTimer(object? s)
    {
        var timer = (SyncTimer)s!;
        if (timer.cancellationToken.IsCancellationRequested) {
            return;
        }
        timer.RaiseTick();
    }
    
    protected override void StartTimer()
    {
        // Reschedules the timer to tick immediately and then resume with framePeriod
        syncTimer.Change(0, period);
    }
    
    protected override  void StopTimer()
    {
        // Disables future ticks for the OS/ThreadPool timer
        syncTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }
    
    protected override void SetTimerPeriod(int newPeriod) {
        period = newPeriod;
        syncTimer.Change(0, newPeriod);
    }
    
    public override void Dispose()
    {
        syncTimer.Dispose();
    }
        
    static SyncTimer()
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
