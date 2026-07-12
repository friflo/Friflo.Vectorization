// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
namespace Friflo.Vectorization.WebGPU;

public struct DrawCommand
{
    public int  vertexCount;
    public int  instanceCount;
    public int  firstVertex;
    public int  firstInstance;

    public DrawCommand(int vertexCount = 0, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0)
    {
        this.vertexCount    = vertexCount;
        this.instanceCount  = instanceCount;
        this.firstVertex    = firstVertex;
        this.firstInstance  = firstInstance;
    }
    
    public DrawCommand()
    {
        instanceCount = 1;
    }
}