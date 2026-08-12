// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.WebGPU;
using Shaders.Imdraw;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public ref struct Draw2D : IDisposable
{
    private readonly    Batcher2D   batcher;
    private             RenderPass  pass;

    
    public void Dispose() {
        Flush();
        pass.Dispose();
    }
    
    internal Draw2D(Batcher2D batcherBatcher, RenderPass pass)
    {
        batcher     = batcherBatcher;
        this.pass   = pass;
    }
    
    public void Rectangle(in Vector2 position, in Vector2 size, uint color)
    {
        DrawQuad(position, size, color);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawQuad(in Vector2 position, in Vector2 size, uint color, GpuTextureView? texture = null)
    {
        var bat = batcher;
        texture ??= bat.defaultWhiteTextureView;
        
        if (bat.vertexCount + 6 > bat.vertexBuffer.Length || (bat.currentTextureView != null && bat.currentTextureView.Value.Handle != texture.Value.Handle)) {
            Flush();
        }
        bat.currentTextureView = texture;

        var span = bat.vertexBuffer.InOut(bat.vertexCount, 6).Span;
        
        // fill Quad with two triangles (TL, TR, BL / TR, BR, BL)
        FillQuad(span, position, size, color);

        bat.vertexCount += 6;
    }
    
    /// <summary>
    /// Fills a 6-vertex span representing two triangles for a quad (TL, TR, BL, TR, BR, BL).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillQuad(Span<Vertex2D> span, in Vector2 position, in Vector2 size, uint color)
    {
        float x1 = position.X;
        float y1 = position.Y;
        float x2 = position.X + size.X;
        float y2 = position.Y + size.Y;

        // V0: Top-Left
        span[0] = new Vertex2D { position = new Vector2(x1, y1), uv = new Vector2(0f, 0f), color = color };
        // V1: Top-Right
        span[1] = new Vertex2D { position = new Vector2(x2, y1), uv = new Vector2(1f, 0f), color = color };
        // V2: Bottom-Left
        span[2] = new Vertex2D { position = new Vector2(x1, y2), uv = new Vector2(0f, 1f), color = color };

        // V3: Top-Right (reused)
        span[3] = span[1];
        // V4: Bottom-Right
        span[4] = new Vertex2D { position = new Vector2(x2, y2), uv = new Vector2(1f, 1f), color = color };
        // V5: Bottom-Left (reused)
        span[5] = span[2];
    }
    
    public void SetViewport(float width, float height)
    {
        if (batcher.vertexCount > 0) {
            Flush();
        }
        var proj = Matrix4x4.CreateOrthographicOffCenter(0f, width, height, 0f, -1f, 1f);
        batcher.uniforms = new ImUniforms { projection = proj };
    }

    public void Flush()
    {
        var bat = batcher;
        if (bat.vertexCount == 0) return;

        bat.vertexBuffer.InOut(0, bat.vertexCount).Write();

        var texture = bat.currentTextureView ?? bat.defaultWhiteTextureView;
        
        Batcher2D.Draw(pass, bat.config, bat.uniforms, texture, bat.defaultSampler, bat.vertexBuffer.In(0, bat.vertexCount));
        
        bat.vertexCount = 0;
    }
}