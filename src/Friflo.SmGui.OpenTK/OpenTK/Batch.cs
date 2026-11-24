// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.OpenTK;

public sealed class OpenTkBatch : SmBatch
{
    private readonly    int vao;
    private readonly    int shaderProgram;
    private readonly    int projLocation;
    private readonly    int texLocation;

    internal OpenTkBatch(OpenTkGuiBackend backend, int maxVertices)
        : base(default, backend, maxVertices, null)
    {
        shaderProgram = backend.shaderProgram;
        projLocation  = GL.GetUniformLocation(shaderProgram, "uProjection");
        texLocation   = GL.GetUniformLocation(shaderProgram, "uTexture");

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        var vbo = ((OpenTkBuffer<Vertex2D>)gpuVertexBuffer).handle;
        var ebo = ((OpenTkBuffer<uint>)gpuIndexBuffer).handle;

        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);

        // Vertex2D layout: position (vec2), uv (vec2), color (4x unorm byte)
        var stride = Marshal.SizeOf<Vertex2D>();
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);

        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 8);

        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.UnsignedByte, true, stride, 16);

        GL.BindVertexArray(0);
    }

    protected override void InitBatch() { }

    public void DrawCommandList(int viewportWidth, int viewportHeight)
    {
        EndBatch();

        GL.Enable(EnableCap.Blend);
        GL.Enable(EnableCap.ScissorTest);
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);

        GL.UseProgram(shaderProgram);
        GL.Uniform1(texLocation, 0);
        GL.ActiveTexture(TextureUnit.Texture0);

        GL.BindVertexArray(vao);

        var scissor = new RectVector2(System.Numerics.Vector2.Zero, viewport);
        GL.Scissor(0, 0, viewportWidth, viewportHeight);

        foreach (var cmd in DrawList)
        {
            if (!cmd.scissor.Equals(scissor)) {
                scissor = cmd.scissor;
                // OpenGL scissor origin is bottom-left
                int scissorY = Math.Max(0, viewportHeight - (int)(scissor.pos.Y + scissor.size.Y));
                GL.Scissor(
                    Math.Max(0, (int)scissor.pos.X),
                    scissorY,
                    Math.Max(0, (int)scissor.size.X),
                    Math.Max(0, (int)scissor.size.Y));
            }

            ApplyBlendState(cmd.blendState);

            var proj = ToOpenTkMatrix(cmd.projection);
            GL.UniformMatrix4(projLocation, false, ref proj);

            var tex = (OpenTkTextureData)cmd.texture.native!;
            GL.BindTexture(TextureTarget.Texture2D, tex.textureId);
            ApplySamplerFilter(tex.textureId, cmd.samplerFilter);

            int indexCount  = cmd.indexView.length;
            int indexOffset = cmd.indexView.offset * sizeof(uint);
            int baseVertex  = cmd.vertexView.offset;

            GL.DrawElementsBaseVertex(
                PrimitiveType.Triangles,
                indexCount,
                DrawElementsType.UnsignedInt,
                indexOffset,
                baseVertex);
        }

        GL.BindVertexArray(0);
        GL.UseProgram(0);
        GL.Disable(EnableCap.ScissorTest);
    }

    private static void ApplyBlendState(BlendState blendState)
    {
        // Reset equation to standard addition before applying custom modes
        GL.BlendEquation(BlendEquationMode.FuncAdd);

        switch (blendState)
        {
            case BlendState.Alpha:
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                break;
            case BlendState.Opaque:
                GL.BlendFunc(BlendingFactor.One, BlendingFactor.Zero);
                break;
            case BlendState.Additive:
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
                break;
            case BlendState.Multiply:
                GL.BlendFunc(BlendingFactor.Zero, BlendingFactor.SrcColor);
                break;
            case BlendState.AddColors:
                GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
                break;
            case BlendState.SubtractColors:
                GL.BlendEquation(BlendEquationMode.FuncReverseSubtract);
                GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
                break;
            default:
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                break;
        }
    }

    private static void ApplySamplerFilter(int textureId, SamplerFilter filter)
    {
        var glFilter = filter == SamplerFilter.Nearest
            ? TextureMinFilter.Nearest
            : TextureMinFilter.Linear;
        var magFilter = filter == SamplerFilter.Nearest
            ? TextureMagFilter.Nearest
            : TextureMagFilter.Linear;

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)glFilter);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)magFilter);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
    }

    private static Matrix4 ToOpenTkMatrix(in System.Numerics.Matrix4x4 m)
    {
        return new Matrix4(
            m.M11, m.M12, m.M13, m.M14,
            m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34,
            m.M41, m.M42, m.M43, m.M44);
    }



    public void Dispose()
    {
        base.Dispose();
        if (vao != 0) {
            GL.DeleteVertexArray(vao);
        }
    }
}