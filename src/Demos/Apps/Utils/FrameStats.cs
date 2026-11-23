using System;
using System.Diagnostics;
using System.Globalization;
using Friflo.SmGui;

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace Demos.Utils;


public class FrameStats
{
    public  TimeSpan    FrameTime => frameTime;
    
    private long        frameStart = Stopwatch.GetTimestamp();
    private TimeSpan    frameTime;
    private long        memStart;
    private long        memDiff;


    public void Update()
    {
        var timestamp   = Stopwatch.GetTimestamp();
        frameTime       = Stopwatch.GetElapsedTime(frameStart, timestamp);
        frameStart      = timestamp;
        
        var mem     = GC.GetAllocatedBytesForCurrentThread();
        memDiff     = mem - memStart;
        memStart    = mem;
    }
    
    public ReadOnlySpan<char> AsSpan(SmBatch batch)
    {
        var sb = batch.StringBuilder();
        sb.Clear().Append($"frame time: ");
        if (batch.Timer.Enabled) {
            sb.Append(CultureInfo.InvariantCulture, $"{FrameTime.TotalMilliseconds,5:F1} ms");
        } else {
            sb.Append($" ---");
        }
        sb.Append($"  alloc: {memDiff,3}");
        return sb.Span();
    }
}