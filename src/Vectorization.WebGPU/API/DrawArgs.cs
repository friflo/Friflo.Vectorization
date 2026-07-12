// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.Vectorization.GPU;


// ReSharper disable once CheckNamespace
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable FieldCanBeMadeReadOnly.Global
namespace Friflo.Vectorization.WebGPU;

public struct DrawArgs
{
    public int  count;
    public int  instanceCount;
    public int  first;
    public int  firstInstance;

    public DrawArgs()
    {
        instanceCount = 1;
    }

    public DrawArgs(int count = 0, int instanceCount = 1, int first = 0, int firstInstance = 0)
    {
        this.count          = count;
        this.instanceCount  = instanceCount;
        this.first          = first;
        this.firstInstance  = firstInstance;
    }
    
    public static DrawArgs InstanceCount<T>(InBuffer<T> buffer) where T : unmanaged
    {
        return new DrawArgs { instanceCount = buffer.Length };
    }
}