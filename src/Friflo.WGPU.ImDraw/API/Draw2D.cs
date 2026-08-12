// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.WebGPU;
using Shaders.Imdraw;

// ReSharper disable RedundantArgumentDefaultValue
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public ref struct Draw2D : IDisposable
{
    private readonly Batcher2D  batcher;
    private          RenderPass pass;

    
    public void Dispose() {
        Flush();
        pass.Dispose();
    }
    
    internal Draw2D(Batcher2D batcherBatcher, RenderPass pass)
    {
        batcher = batcherBatcher;
        this.pass = pass;
    }
    
    public void Rectangle(in Vector2 position, in Vector2 size, uint color)
    {
        DrawQuad(position, size, new Vector2(0f, 0f), new Vector2(1f, 1f), color, null);
    }
    
    /// <summary>
    /// Draws a sprite using normal 0..1 UV coordinates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(in Vector2 position, in Vector2 size, GpuTextureView texture, uint color = 0xFFFFFFFF, in Vector2 uvMin = default, Vector2 uvMax = default)
    {
        if (uvMax == default) uvMax = new Vector2(1f, 1f);
        DrawQuad(position, size, uvMin, uvMax, color, texture);
    }

    /// <summary>
    /// Draws a sub-region (source rect in pixels) from a texture/spritesheet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(in Vector2 position, in Vector2 size, GpuTextureView texture, in Vector2 sourceRectPos, in Vector2 sourceRectSize, in Vector2 textureSize, uint color = 0xFFFFFFFF)
    {
        Vector2 uvMin = sourceRectPos / textureSize;
        Vector2 uvMax = (sourceRectPos + sourceRectSize) / textureSize;
        DrawQuad(position, size, uvMin, uvMax, color, texture);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawQuad(in Vector2 position, in Vector2 size, in Vector2 uvMin, in Vector2 uvMax, uint color, GpuTextureView? texture = null)
    {
        var bat = batcher;
        var texView = texture ?? bat.defaultWhiteTextureView;
        
        // flush if full (4 vertices per quad) or texture changed
        if (bat.vertexCount + 4 > bat.vertexBuffer.Length ||
            (bat.currentTextureView.HasValue && bat.currentTextureView.Value.Handle != texView.Handle))
        {
            Flush();
        }
        bat.currentTextureView = texView;

        var span = bat.vertexBuffer.InOut(bat.vertexCount, 4).Span;
        
        float x1 = position.X;
        float y1 = position.Y;
        float x2 = position.X + size.X;
        float y2 = position.Y + size.Y;

        // V0: Top-Left
        span[0] = new Vertex2D { position = new Vector2(x1, y1), uv = uvMin,                            color = color };
        // V1: Top-Right
        span[1] = new Vertex2D { position = new Vector2(x2, y1), uv = new Vector2(uvMax.X, uvMin.Y),    color = color };
        // V2: Bottom-Right
        span[2] = new Vertex2D { position = new Vector2(x2, y2), uv = uvMax,                            color = color };
        // V3: Bottom-Left
        span[3] = new Vertex2D { position = new Vector2(x1, y2), uv = new Vector2(uvMin.X, uvMax.Y),    color = color };

        bat.vertexCount += 4;
    }
    
    public void SetViewport(float width, float height)
    {
        if (batcher.vertexStart != batcher.vertexCount) {
            Flush();
        }
        var proj = Matrix4x4.CreateOrthographicOffCenter(0f, width, height, 0f, -1f, 1f);
        batcher.uniforms = new ImUniforms { projection = proj };
    }

    public void Flush()
    {
        var bat = batcher;
        if (batcher.vertexStart == bat.vertexCount) {
            return;
        }
        
        int quadCount  = bat.vertexCount / 4;
        int indexCount = quadCount * 6;

        bat.vertexBuffer.InOut(0, bat.vertexCount).Write();

        var texture    = bat.currentTextureView ?? bat.defaultWhiteTextureView;
        var vertexView = bat.vertexBuffer.In(bat.vertexStart, bat.vertexCount);
        var indexView  = bat.indexBuffer.In(0, indexCount);
        
        Batcher2D.Draw(pass, bat.config, bat.uniforms, texture, bat.defaultSampler, vertexView, indexView);
        
        bat.vertexStart = bat.vertexCount;
    }
}