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


internal sealed class ThreadTimer : SmTimer
{
    /// Type: <see cref="System.Threading.Timer"/>
    private readonly    Timer               systemTimer;
    private readonly    CancellationToken   cancellationToken = CancellationToken.None;
    private             int                 period;
    
    internal ThreadTimer(int period)
    {
        this.period = period;
        systemTimer = new Timer(OnTimerCallback, this, dueTime: Timeout.Infinite, period: Timeout.Infinite);
    }
    
    private static void OnTimerCallback(object? s)
    {
        var timer = (ThreadTimer)s!;
        if (timer.cancellationToken.IsCancellationRequested) {
            return;
        }
        timer.RaiseTick();
    }
    
    protected override void StartTimer()
    {
        // Reschedules the timer to tick immediately and then resume with framePeriod
        systemTimer.Change(0, period);
    }
    
    protected override  void StopTimer()
    {
        // Disables future ticks for the OS/ThreadPool timer
        systemTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }
    
    protected override void SetTimerPeriod(int newPeriod) {
        period = newPeriod;
        systemTimer.Change(0, newPeriod);
    }
    
    public override void Dispose()
    {
        systemTimer.Dispose();
    }
        
    static ThreadTimer()
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
