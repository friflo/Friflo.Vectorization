// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Shaders.Imdraw;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public sealed partial class Batcher2D : IDisposable
{
    private readonly    GpuBuffer<Vertex2D> vertexBuffer;
//  private readonly    ushort[]            indices = [];
    private             int                 vertexCount;
    private             ImUniforms          uniforms;
    
    public Batcher2D(WgpuDevice device, int maxVertices = 60_000)
    {
        vertexBuffer = device.CreateBuffer<Vertex2D>(maxVertices, default, "Batcher2D", BufferProfile.InOut); 
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawQuad(in Vector2 position, in Vector2 size, uint color)
    {
        if (vertexCount + 6 > vertexBuffer.Length) {
            Flush();
        }

        var span = vertexBuffer.InOut(vertexCount, 6).Span;
        
        // fill Quad with two triangles (TL, TR, BL / TR, BR, BL)
        FillQuad(span, position, size, color);

        vertexCount += 6;
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
        span[0] = new Vertex2D { Position = new Vector2(x1, y1), UV = new Vector2(0f, 0f), Color = color };
        // V1: Top-Right
        span[1] = new Vertex2D { Position = new Vector2(x2, y1), UV = new Vector2(1f, 0f), Color = color };
        // V2: Bottom-Left
        span[2] = new Vertex2D { Position = new Vector2(x1, y2), UV = new Vector2(0f, 1f), Color = color };

        // V3: Top-Right (reused)
        span[3] = span[1];
        // V4: Bottom-Right
        span[4] = new Vertex2D { Position = new Vector2(x2, y2), UV = new Vector2(1f, 1f), Color = color };
        // V5: Bottom-Left (reused)
        span[5] = span[2];
    }

    public void Flush()
    {
        if (vertexCount == 0) return;

        vertexBuffer.InOut(0, vertexCount).Write();

        // call Draw Shader
        Draw(default, default, uniforms, default, default, vertexBuffer.In());
        
        vertexCount = 0;
    }
    
    [Shader("~/shaders/imdraw/draw2d.wgsl", vertex: "vs_main", fragment: "fs_main")]
    private static partial void Draw(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]                   in ImUniforms       globals,
        [Map(0, 1)] [texture_2d(ST.f32)]        GpuTextureView      texture,
        [Map(0, 2)] [sampler]                   GpuSampler          sampler,
                    [VertexBuffer(0)] [Draw]    InBuffer<Vertex2D>  vertices);
    
    // [ ]  If needed, add parameter: [IndexBuffer] InBuffer<ushort|uint> indices.


    public void Dispose() => vertexBuffer.Dispose();
}