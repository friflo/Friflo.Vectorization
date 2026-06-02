// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

public readonly struct GpuQueue
{
    private readonly PipelineContext context;
    
    public void ReadBuffers() => context.ReadBuffers();
    
    internal GpuQueue(PipelineContext context) {
        this.context = context;
    }
}
