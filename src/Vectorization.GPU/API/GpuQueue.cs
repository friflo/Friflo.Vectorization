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
    protected internal virtual  void        ReadBuffers()   { }
    protected internal virtual  QueueStats  GetQueueStats() => default;
}


public readonly struct GpuQueue
{
    private readonly    CommandStream   commandStream;
    public              QueueStats      Stats       => commandStream.GetQueueStats();
    
    public override     string          ToString()  => $"Commands: {Stats.Commands}  Ranges: {Stats.Ranges}";

    
    public void     ReadBuffers()   => commandStream.ReadBuffers();
    public void     Submit()        { }                     // TODO
    
//  public void     Synchronize()   { }  ???

    
    internal GpuQueue(CommandStream commandStream) {
        this.commandStream = commandStream;
    }
}
