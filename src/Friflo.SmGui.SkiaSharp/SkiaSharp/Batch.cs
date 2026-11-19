// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;
using SkiaSharp;

// ReSharper disable InconsistentNaming
// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.SkiaSharp;


public sealed class SkiaSharpBatch : SmBatch
{
    internal SkiaSharpBatch(SkiaSharpGuiBackend backend, int maxVertices)
        : base(default, backend, maxVertices, null)
    { }

    protected override void InitBatch() { }
    
    public void DrawCommandList(SKCanvas canvas, int x, int y, int width, int height)
    {
        EndBatch();

        canvas.Save();
        canvas.ClipRect(new SKRect(x, y, x + width, y + height));

        var vertexBuffer = (SkiaSharpVertexBuffer)gpuVertexBuffer;
        var indexBuffer  = (SkiaSharpIndexBuffer)gpuIndexBuffer;

        gpuVertexBuffer.Write(0, Vertices.Length);

        var vertices = vertexBuffer.Memory.Span;
        var indices  = indexBuffer.Memory.Span;

        using var paint = new SKPaint();
        paint.IsAntialias = false;

        foreach (var cmd in DrawList)
        {
            canvas.Save();
            // TODO  use cmd.projection (Matrix4x4)
            _ = cmd.projection;
            
            var pos  = cmd.scissor.pos;
            var size = cmd.scissor.size;
            canvas.ClipRect(new SKRect(pos.X, pos.Y, pos.X + size.X, pos.Y + size.Y));

            var bitmap   = (SKBitmap)cmd.texture.native!;
            var vertexView = cmd.vertexView;
            var indexView  = cmd.indexView;

            paint.Shader = SKShader.CreateBitmap(bitmap, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);

            int vCount = vertexView.length;
            int iCount = indexView.length;

            SKPoint[] positions = new SKPoint[vCount];
            SKPoint[] texs      = new SKPoint[vCount];
            SKColor[] colors    = new SKColor[vCount];
            ushort[]  skIndices = new ushort[iCount];

            for (int i = 0; i < vCount; i++)
            {
                var v = vertices[vertexView.offset + i];
                positions[i] = new SKPoint(v.position.X, v.position.Y);
                // todo  remove - seems to be a workaround
                // Convert normalized UVs (0..1) to pixel coordinates if needed  - 
                texs[i]      = new SKPoint(v.uv.X * bitmap.Width, v.uv.Y * bitmap.Height);
                // texs[i]      = new SKPoint(v.uv.X, v.uv.Y);
                
                colors[i]    = new SKColor(v.color);
            }

            for (int i = 0; i < iCount; i++)
            {
                skIndices[i] = (ushort)(indices[indexView.offset + i] - vertexView.offset);
            }

            canvas.DrawVertices(SKVertexMode.Triangles, positions, texs, colors, skIndices, paint);

            canvas.Restore();
        }

        canvas.Restore();
    }
    
    static SKMatrix ToSKMatrix(in Matrix4x4 m) => new SKMatrix
    {
        ScaleX = m.M11,
        SkewX  = m.M21,
        TransX = m.M41,

        SkewY  = m.M12,
        ScaleY = m.M22,
        TransY = m.M42,

        Persp0 = m.M14,
        Persp1 = m.M24,
        Persp2 = m.M44 == 0 ? 1f : m.M44
    };
}