// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


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

    public DrawArgs(int count = 0, int instanceCount = 1, int first = 0, int firstInstance = 0)
    {
        this.count          = count;
        this.instanceCount  = instanceCount;
        this.first          = first;
        this.firstInstance  = firstInstance;
    }
    
    public DrawArgs()
    {
        instanceCount = 1;
    }
}