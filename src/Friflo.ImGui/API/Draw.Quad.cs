// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable MergeIntoPattern
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


public readonly ref partial struct ImDraw
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillQuad(Vector2 v0, Vector2 v1, Vector2 v2, Vector2 v3, Color32 color)
    {
        var bat = batch;
        if (bat.vertexCount + 4 > bat.vertexBuffer.Length || !bat.currentTexture.hasWhitePixel) {
            bat.Flush();
            bat.currentTexture = bat.defaultFontTexture;
        }
        var uv = bat.currentTexture.whiteUv;

        var packed   = color.Packed;
        ref var quad = ref AddQuad();
        quad[0] = new Vertex2D(v0, uv, packed);
        quad[1] = new Vertex2D(v1, uv, packed);
        quad[2] = new Vertex2D(v2, uv, packed);
        quad[3] = new Vertex2D(v3, uv, packed);
    }
    
    public void DrawQuad(in VertexQuad quad, in ImTexture texture)
    {
        var bat = batch;
        if (bat.vertexCount + 4 > bat.vertexBuffer.Length || bat.currentTexture != texture) {
            bat.Flush();
            bat.currentTexture = texture;
        }
        ref var targetQuad = ref AddQuad();
        targetQuad = quad;
    }
    
    public void DrawQuad(in Vertex2D v0, in Vertex2D v1, in Vertex2D v2, in Vertex2D v3, in ImTexture texture)
    {
        var bat = batch;
        if (bat.vertexCount + 4 > bat.vertexBuffer.Length || bat.currentTexture != texture) {
            bat.Flush();
            bat.currentTexture = texture;
        }
        ref var quad = ref AddQuad();
        quad[0] = v0;
        quad[1] = v1;
        quad[2] = v2;
        quad[3] = v3;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawQuads(ReadOnlySpan<Vertex2D> vertices, in ImTexture texture)
    {
        if ((vertices.Length & 3) != 0) {
            ThrowInvalidVertexCount(vertices.Length);
        }
        if (vertices.IsEmpty) return;

        var bat = batch;
        if (bat.currentTexture != texture) {
            bat.Flush();
            bat.currentTexture = texture;
        }
        while (vertices.Length > 0)
        {
            int availableSpace = bat.vertexBuffer.Length - bat.vertexCount;

            if (availableSpace < 4) {
                bat.Flush();
                availableSpace = bat.vertexBuffer.Length;
            }
            int copyCount = Math.Min(vertices.Length, availableSpace);

            var destination = bat.vertexBuffer.Span.Slice(bat.vertexCount, copyCount);
            vertices[..copyCount].CopyTo(destination);

            bat.vertexCount += copyCount;
            vertices = vertices[copyCount..];
        }
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidVertexCount(int length)
    {
        throw new ArgumentException($"Number of vertices must be divisible by 4. Was: {length}.");
    }

    
    /* [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<Vertex2D> AddQuad() {
        var start = batch.vertexCount;
        batch.vertexCount = start + 4;
        return batch.vertexBuffer.Span.Slice(start, 4);
    } */
    
    /// <summary>
    /// Returns a <see cref="VertexQuad"/> <br/>
    /// [0] Top-Left   [1] Top-Right   [2] Bottom-Right   [3] Bottom-Left
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref VertexQuad AddQuad() {
        var start = batch.vertexCount;
        batch.vertexCount = start + 4;
        ref var firstVertex = ref MemoryMarshal.GetReference(batch.vertexBuffer.Span.Slice(start));
        return ref Unsafe.As<Vertex2D, VertexQuad>(ref firstVertex);
    }
}

