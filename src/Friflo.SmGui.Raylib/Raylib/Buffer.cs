// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Raylib;

internal sealed class RaylibVertexBuffer : SmBuffer<Vertex2D>
{
    private readonly Vertex2D[] vertexData;

    public override Memory<Vertex2D> Memory => vertexData;

    public RaylibVertexBuffer(int vertexCount) {
        vertexData = new Vertex2D[vertexCount];
    }

    public override void Dispose() {
        // No dedicated unmanaged GPU buffer needed; rlgl batches vertices directly in DrawCommandList.
    }

    public override void Write(int start, int length) {
        // Direct span access via AsSpan() in DrawCommandList avoids extra staging copies.
    }

    internal ReadOnlySpan<Vertex2D> AsSpan() => vertexData;
}

internal sealed class RaylibIndexBuffer : SmBuffer<uint>
{
    private readonly uint[] indexData;

    public override Memory<uint> Memory => indexData;

    public RaylibIndexBuffer(int indexCount) {
        indexData = new uint[indexCount];
    }

    public override void Dispose() {
        // No dedicated unmanaged GPU buffer needed; rlgl batches indices directly in DrawCommandList.
    }

    public override void Write(int start, int length) {
        // Direct span access via AsSpan() in DrawCommandList avoids extra staging copies.
    }

    internal ReadOnlySpan<uint> AsSpan() => indexData;
}