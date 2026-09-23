// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;


// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;

internal sealed class FrameTimer
{
    private readonly    TmClient        client;
    private             Timer?          syncTimer;
    private             PeriodicTimer?  asyncTimer;
    private             int             framePeriod; // ms
    private readonly    bool            isSync;
    
    internal FrameTimer(TmClient client, int framePeriod, bool isSync) {
        this.client         = client;
        this.framePeriod    = framePeriod;
        this.isSync         = isSync;
    }
    
    internal void SetPeriod(int period)
    {
        if (isSync) {
            syncTimer?.Change(0, period);
        } else {
            asyncTimer?.Period = TimeSpan.FromMilliseconds(period);
        }
        framePeriod = period;
    }
    
    private async Task StartFrameTickerAsync(TmSessionLoop loop, CancellationToken cancellationToken)
    {
        asyncTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(framePeriod));
        try
        {
            while (await asyncTimer.WaitForNextTickAsync(cancellationToken))
            {
                // Clean, non-blocking, and zero-allocation
                loop.TryEnqueueEvent(client, ClientEventType.FrameTick, default);
            }
        }
        catch (OperationCanceledException) {
            // Expected behavior on shutdown
        }
    }
    
    
    private void StartFrameTickerSync(TmSessionLoop loop, CancellationToken cancellationToken)
    {
        syncTimer = new Timer(_ => {
            if (cancellationToken.IsCancellationRequested) return;

            // Clean, lock-free thread-safe call from the ThreadPool
            loop.TryEnqueueEvent(client, ClientEventType.FrameTick, default);
        }, null, dueTime: 0, period: framePeriod);
    }
}