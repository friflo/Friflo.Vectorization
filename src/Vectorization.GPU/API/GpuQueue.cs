// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;


public struct QueueStats
{
    public int  Commands;
    public int  Ranges;
}

public readonly struct GpuQueue
{
    public          QueueStats  Stats => context.GetQueueStats();
    
    public override string      ToString()  => $"Commands: {Stats.Commands}  Ranges: {Stats.Ranges}";

    private readonly PipelineContext context;
    
    public void     ReadBuffers()   => context.ReadBuffers();
    public void     Submit()        { }                     // TODO
    
    internal GpuQueue(PipelineContext context) {
        this.context = context;
    }
}
