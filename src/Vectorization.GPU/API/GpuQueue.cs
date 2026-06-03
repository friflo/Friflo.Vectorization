// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;


public struct QueueStats
{
    public int  Commands;
    public int  Ranges;
}

public abstract class CommandStream
{
    protected internal virtual  void        ReadBuffers()                   { }
    protected internal virtual  QueueStats  GetQueueStats()                 => default;
    protected internal virtual  void        FlushTo(GpuQueue targetQueue)   { }
}


public readonly struct GpuQueue
{
    public readonly CommandStream   CommandStream;
    public          QueueStats      Stats => CommandStream.GetQueueStats();
    
    public override string      ToString()  => $"Commands: {Stats.Commands}  Ranges: {Stats.Ranges}";

    
    public void     ReadBuffers()                   => CommandStream.ReadBuffers();
    public void     Submit()                        { }                     // TODO
    public void     FlushTo(GpuQueue targetQueue)   => CommandStream.FlushTo(targetQueue);
    
//  public void     Synchronize()                       { }  ???

    
    internal GpuQueue(CommandStream commandStream) {
        CommandStream = commandStream;
    }
}
