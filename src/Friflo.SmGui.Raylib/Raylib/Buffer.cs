// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Graphics;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Raylib;

internal sealed class RaylibVertexBuffer : SmBuffer<Vertex2D>
{
    internal readonly VertexBuffer      vertexBuffer;
    private  readonly Vertex2D[]        vertexData;
    
    public   override Memory<Vertex2D>  Memory => vertexData;
    
    public RaylibVertexBuffer(VertexBuffer buffer) {
        vertexBuffer    = buffer;
        vertexData      = new Vertex2D[buffer.VertexCount];
    }

    public override void Dispose() {
        vertexBuffer.Dispose();
    }
    
    public override void Write(int start, int length) {
        vertexBuffer.SetData(vertexData, start, length);
    }

    internal static readonly VertexDeclaration VertexDeclaration = new(
        Unsafe.SizeOf<Vertex2D>(),
        // Vertex2D.position
        new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0),
        // Vertex2D.uv
        new VertexElement(8, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        // Vertex2D.color (Rgba8Pack)
        new VertexElement(16, VertexElementFormat.Color, VertexElementUsage.Color, 0)
    );
}


internal sealed class RaylibIndexBuffer : SmBuffer<uint>
{
    internal readonly IndexBuffer   indexBuffer;
    private  readonly uint[]        indexData;
    
    public   override Memory<uint>  Memory => indexData;
    
    public RaylibIndexBuffer(IndexBuffer buffer) {
        indexBuffer = buffer;
        indexData   = new uint[buffer.IndexCount];
    }

    public override void Dispose() {
        indexBuffer.Dispose();
    }
    
    public override void Write(int start, int length) {
        indexBuffer.SetData(indexData, start, length);
    }
}