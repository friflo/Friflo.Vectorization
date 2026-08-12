// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.WebGPU;


// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public ref partial struct Draw2D
{
    /// <summary>
    /// Draws a sprite using normal 0..1 UV coordinates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(in Vector2 position, in Vector2 size, GpuTextureView texture, uint color = 0xFFFFFFFF, in Vector2 uvMin = default, Vector2 uvMax = default)
    {
        if (uvMax == default) uvMax = new Vector2(1f, 1f);
        DrawQuadUv(position, size, uvMin, uvMax, color, texture);
    }

    /// <summary>
    /// Draws a sub-region (source rect in pixels) from a texture/spritesheet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(in Vector2 position, in Vector2 size, GpuTextureView texture, in Vector2 sourceRectPos, in Vector2 sourceRectSize, in Vector2 textureSize, uint color = 0xFFFFFFFF)
    {
        Vector2 uvMin = sourceRectPos / textureSize;
        Vector2 uvMax = (sourceRectPos + sourceRectSize) / textureSize;
        DrawQuadUv(position, size, uvMin, uvMax, color, texture);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawQuadUv(in Vector2 position, in Vector2 size, in Vector2 uvMin, in Vector2 uvMax, uint color, GpuTextureView texture)
    {
        var bat = batcher;
        
        if (bat.vertexCount + 4 > bat.vertexBuffer.Length || (bat.currentTextureView != null && bat.currentTextureView.Value.Handle != texture.Handle)) {
            Flush();
        }
        bat.currentTextureView = texture;

        var span = bat.vertexBuffer.InOut(bat.vertexCount, 4).Span;
        FillQuadUv(span, position, size, uvMin, uvMax, color);

        bat.vertexCount += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillQuadUv(Span<Vertex2D> span, in Vector2 position, in Vector2 size, in Vector2 uvMin, in Vector2 uvMax, uint color)
    {
        float x1 = position.X;
        float y1 = position.Y;
        float x2 = position.X + size.X;
        float y2 = position.Y + size.Y;

        // V0: Top-Left
        span[0] = new Vertex2D { position = new Vector2(x1, y1), uv = uvMin, color = color };
        // V1: Top-Right
        span[1] = new Vertex2D { position = new Vector2(x2, y1), uv = new Vector2(uvMax.X, uvMin.Y), color = color };
        // V2: Bottom-Right
        span[2] = new Vertex2D { position = new Vector2(x2, y2), uv = uvMax, color = color };
        // V3: Bottom-Left
        span[3] = new Vertex2D { position = new Vector2(x1, y2), uv = new Vector2(uvMin.X, uvMax.Y), color = color };
    }
    
}