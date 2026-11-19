// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Raylib_cs;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Raylib;


public sealed class RaylibBatch : SmBatch
{
    private RectVector2 lastScissor;
    private bool scissorActive;

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
        Rlgl.Viewport(x, y, width, height);

        scissorActive   = false;
        uint currentTex = uint.MaxValue;

        foreach (var cmd in DrawList) {
            UpdateScissor(cmd.scissor, x, y, width, height);

            var texture = cmd.texture.AsGpuTexture();
            if (texture.Id != currentTex) {
                // Flush before switching texture id inside rlgl.
                Rlgl.DrawRenderBatchActive();
                Rlgl.SetTexture(texture.Id);
                currentTex = texture.Id;
            }

            var filter = cmd.samplerFilter == SamplerFilter.Linear ? 0x2601 : 0x2600;
            Rlgl.TextureParameters(texture.Id, 0x2800, filter);
            Rlgl.TextureParameters(texture.Id, 0x2801, filter);

            var vertexView  = cmd.vertexView;
            var indexView   = cmd.indexView;

            Rlgl.Begin(DrawMode.Triangles);
            for (int i = 0; i < indexView.length; i++) {
                uint idx = indices[indexView.offset + i];
                ref readonly var v = ref vertices[vertexView.offset + (int)idx];

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
            Rlgl.End();
        }

        if (scissorActive) {
            Rlgl.DrawRenderBatchActive();
            Rlgl.DisableScissorTest();
            scissorActive = false;
        }

        Rlgl.DrawRenderBatchActive();
        Rlgl.SetTexture(0);
    }

    private void UpdateScissor(in RectVector2 scissor, int vpX, int vpY, int vpWidth, int vpHeight) {
        bool fullViewport =
            scissor.pos.X <= 0f && scissor.pos.Y <= 0f &&
            scissor.size.X >= vpWidth && scissor.size.Y >= vpHeight;

        if (fullViewport) {
            if (scissorActive) {
                Rlgl.DrawRenderBatchActive();
                Rlgl.DisableScissorTest();
                scissorActive = false;
            }
            return;
        }

        if (!scissorActive || !scissor.Equals(lastScissor)) {
            int sx = vpX + (int)scissor.pos.X;
            int sy = vpY + (int)scissor.pos.Y;
            int sw = Math.Max(0, (int)scissor.size.X);
            int sh = Math.Max(0, (int)scissor.size.Y);

            // Flush before changing scissor state so previous commands keep their clip rect.
            Rlgl.DrawRenderBatchActive();
            if (!scissorActive) {
                Rlgl.EnableScissorTest();
                scissorActive = true;
            }
            Rlgl.Scissor(sx, sy, sw, sh);
            lastScissor = scissor;
        }
    }
}