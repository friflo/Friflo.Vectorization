// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// file: Buffer.cs
using System;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Godot;

// Godot has no vertex / index buffer API for canvas items.
// The data stays in CPU memory and is passed to RenderingServer.CanvasItemAddTriangleArray() in GodotBatch.
internal sealed class GodotVertexBuffer : SmBuffer<Vertex2D>
{
    internal readonly Vertex2D[]        vertexData;
    
    public   override Memory<Vertex2D>  Memory => vertexData;
    
    public GodotVertexBuffer(int vertexCount) {
        vertexData = new Vertex2D[vertexCount];
    }

    public override void Dispose() { }
    
    public override void Write(int start, int length) { }
}


internal sealed class GodotIndexBuffer : SmBuffer<uint>
{
    internal readonly uint[]        indexData;
    
    public   override Memory<uint>  Memory => indexData;
    
    public GodotIndexBuffer(int indexCount) {
        indexData = new uint[indexCount];
    }

    public override void Dispose() { }
    
    public override void Write(int start, int length) { }
}
