// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// Buffer.cs
using System;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.SkiaSharp;

internal sealed class SkiaSharpVertexBuffer : SmBuffer<Vertex2D>
{
    private  readonly Vertex2D[]        vertexData;
    
    public   override Memory<Vertex2D>  Memory => vertexData;
    
    public SkiaSharpVertexBuffer(int vertexCount) {
        vertexData      = new Vertex2D[vertexCount];
    }

    public override void Dispose() { }
    
    public override void Write(int start, int length) {
        // SkiaSharp reads directly from managed arrays, no explicit GPU upload required.
    }
}


internal sealed class SkiaSharpIndexBuffer : SmBuffer<uint>
{
    private  readonly uint[]        indexData;
    
    public   override Memory<uint>  Memory => indexData;
    
    public SkiaSharpIndexBuffer(int indexCount) {
        indexData   = new uint[indexCount];
    }

    public override void Dispose() { }
    
    public override void Write(int start, int length) {
        // SkiaSharp reads directly from managed arrays, no explicit GPU upload required.
    }
}