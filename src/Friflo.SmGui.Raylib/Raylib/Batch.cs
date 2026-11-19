// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

// ReSharper disable MergeIntoPattern
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Raylib;


public sealed class RaylibBatch : SmBatch
{
    private RectVector2 lastScissor;
    private bool        scissorActive;

    internal RaylibBatch(RaylibGuiBackend backend, int maxVertices)
        : base(default, backend, maxVertices, null) {
    }

    protected override void InitBatch() {
    }

    public void DrawCommandList(int x, int y, int width, int height)
    {
        EndBatch();

        var vertices    = ((RaylibVertexBuffer)gpuVertexBuffer).AsSpan();
        var indices     = ((RaylibIndexBuffer)gpuIndexBuffer).AsSpan();

        // Flush pending rlgl vertices before applying custom viewport and scissor state.
        Rlgl.DrawRenderBatchActive();
        // Rlgl.DisableBackfaceCulling();  - not required. EmitVertex() uses correct triangle winding.
        Rlgl.Viewport(x, y, width, height);

        scissorActive = false;
        var previousProjection = Rlgl.GetMatrixProjection();

        foreach (DrawCommand cmd in DrawList)
        {
            UpdateScissor(cmd.scissor, x, y, width, height);
            // Apply command projection (already mapped to normalized clip space for viewport size).
            Rlgl.SetMatrixProjection(Matrix4x4.Transpose(cmd.projection));

            var texture = cmd.texture.AsGpuTexture();
            Rlgl.SetTexture(texture.Id);

            var filter = cmd.samplerFilter == SamplerFilter.Linear ? 0x2601 : 0x2600;
            Rlgl.TextureParameters(texture.Id, 0x2800, filter);
            Rlgl.TextureParameters(texture.Id, 0x2801, filter);

            var vertexView  = cmd.vertexView;
            var indexView   = cmd.indexView;

            // TODO  could optimize performance with Rlgl.DrawVertexArrayElements() to minimize Rlgl calls from N to 1
            Rlgl.Begin(DrawMode.Triangles);
            for (int i = 0; i < indexView.length; i += 3) {
                // Reverse triangle winding (0, 2, 1) to match rlgl front-face culling.
                EmitVertex(vertices, vertexView.offset, indices[indexView.offset + i]);
                EmitVertex(vertices, vertexView.offset, indices[indexView.offset + i + 2]);
                EmitVertex(vertices, vertexView.offset, indices[indexView.offset + i + 1]);
            }
            Rlgl.End();
         
            Rlgl.DrawRenderBatchActive(); // flush after every DrawCommand 
        }

        Rlgl.SetMatrixProjection(previousProjection);

        if (scissorActive) {
            Rlgl.DisableScissorTest();
            scissorActive = false;
        }
        Rlgl.SetTexture(0);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EmitVertex(ReadOnlySpan<Vertex2D> vertices, int baseVertex, uint index)
    {
        ref readonly var v = ref vertices[baseVertex + (int)index];

        // Pack color bytes directly into R, G, B, A for minimal overhead.
        uint packedColor = v.color;
        byte r = (byte)(packedColor & 0xFF);
        byte g = (byte)((packedColor >> 8) & 0xFF);
        byte b = (byte)((packedColor >> 16) & 0xFF);
        byte a = (byte)((packedColor >> 24) & 0xFF);
        
        Rlgl.Color4ub(r, g, b, a);
        Rlgl.TexCoord2f(v.uv.X, v.uv.Y);
        Rlgl.Vertex2f(v.position.X, v.position.Y);
    }

    private void UpdateScissor(in RectVector2 scissor, int vpX, int vpY, int vpWidth, int vpHeight)
    {
        bool fullViewport = scissor.pos.X  <= 0f      && scissor.pos.Y  <= 0f &&
                            scissor.size.X >= vpWidth && scissor.size.Y >= vpHeight;

        if (fullViewport) {
            if (!scissorActive) {
                return;
            }
            Rlgl.DisableScissorTest();
            scissorActive = false;
            return;
        }

        if (scissorActive && scissor.Equals(lastScissor)) {
            return;
        }
        int sx = vpX + (int)scissor.pos.X;
        // Flip Y for OpenGL/rlgl bottom-left scissor origin relative to viewport/screen height.
        int sy = (vpY + vpHeight) - ((int)scissor.pos.Y + (int)scissor.size.Y);
        int sw = Math.Max(0, (int)scissor.size.X);
        int sh = Math.Max(0, (int)scissor.size.Y);

        if (!scissorActive) {
            Rlgl.EnableScissorTest();
            scissorActive = true;
        }
        Rlgl.Scissor(sx, sy, sw, sh);
        lastScissor = scissor;
    }
}