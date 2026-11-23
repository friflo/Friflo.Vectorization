using System;
using System.Diagnostics;
using System.Globalization;
using Friflo.SmGui;

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace Demos.Utils;


public class FrameStats
{
    public  double  FrameTime => frameTime;
    
    private long    frameStart = Stopwatch.GetTimestamp();
    private double  frameTime;
    private long    memStart;
    private long    memDiff;


    public void Update()
    {
        var timestamp   = Stopwatch.GetTimestamp();
        frameTime       = Stopwatch.GetElapsedTime(frameStart, timestamp).TotalMilliseconds;
        frameStart      = timestamp;
        
        var mem     = GC.GetAllocatedBytesForCurrentThread();
        memDiff     = mem - memStart;
        memStart    = mem;
    }
    
    public ReadOnlySpan<char> AsSpan(Gui gui)
    {
        var sb = gui.widget.StringBuilder();
        sb.Clear().Append($"frame time: ");
        if (gui.Batch.Timer.Enabled) {
            sb.Append(CultureInfo.InvariantCulture, $"{frameTime,5:F1} ms");
        } else {
            sb.Append($" ---");
        }
        sb.Append($"  alloc: {memDiff,3}");
        return sb.Span();
    }
}