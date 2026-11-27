// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using SkiaSharp;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.SkiaSharp;


public sealed class SkiaSharpBatch : SmBatch
{
    private readonly SKPaint                    paint;
    private readonly Dictionary<nint, SKShader> bitmapShader = new();
        
    internal SkiaSharpBatch(SkiaSharpGuiBackend backend, int maxVertices)
        : base(default, backend, maxVertices, null)
    {
        paint = new SKPaint { IsAntialias = false };
    }
    
    public void Destroy() {
        foreach (var shader in bitmapShader.Values) {
            shader.Dispose();   
        }
        Dispose();
        paint.Dispose();
    }

    protected override void InitBatch() { }
    
    public void DrawCommandList(SKCanvas canvas, int x, int y, int width, int height)
    {
        EndBatch();

        canvas.Save();
        canvas.ClipRect(new SKRect(x, y, x + width, y + height));   // in device space, before the transform

        // Map clip space [-1..1] (Y up) into target rect (x, y, width, height) in canvas pixels (Y down).
        var targetRectMatrix =
            Matrix4x4.CreateScale(width * 0.5f, -height * 0.5f, 1f) *
            Matrix4x4.CreateTranslation(x + width * 0.5f, y + height * 0.5f, 0f);

        var vertexBuffer = (SkiaSharpVertexBuffer)gpuVertexBuffer;
        var indexBuffer  = (SkiaSharpIndexBuffer) gpuIndexBuffer;
        gpuVertexBuffer.Write(0, Vertices.Length);
        var vertices = vertexBuffer.Memory.Span;
        var indices  = indexBuffer. Memory.Span;

        foreach (var cmd in DrawList)
        {
            var indexView  = cmd.indexView;
            var vertexView = cmd.vertexView;
            int iCount     = indexView.length;
            if (iCount == 0) {
                continue;
            }
            canvas.Save();

            // Combine command projection with target rect mapping.
            var m = cmd.projection * targetRectMatrix;
            var skMatrix = new SKMatrix(
                m.M11, m.M21, m.M41,
                m.M12, m.M22, m.M42,
                m.M14, m.M24, m.M44
            );

            // indices are relative to vertexView.offset (like baseVertex) -> no subtraction
            var skIndices = new ushort[iCount];
            uint maxIndex  = 0;
            for (int i = 0; i < iCount; i++) {
                var index    = indices[indexView.offset + i];
                skIndices[i] = (ushort)index;
                if (index > maxIndex) maxIndex = index;
            }
            uint vCount = maxIndex + 1;

            var bitmap    = (SKBitmap)cmd.texture.native!;
            var positions = new SKPoint[vCount];
            var texs      = new SKPoint[vCount];
            var colors    = new SKColor[vCount];
            for (int i = 0; i < vCount; i++) {
                ref readonly var v = ref vertices[vertexView.offset + i];
                positions[i] = new SKPoint(v.position.X, v.position.Y);
                texs[i]      = new SKPoint(v.uv.X * bitmap.Width, v.uv.Y * bitmap.Height);   // Skia: bitmap shader uses pixel coords
                colors[i]    = ToSKColor(v.color);
            }

            var pos  = cmd.scissor.pos;
            var size = cmd.scissor.size;
            canvas.ClipRect(new SKRect(pos.X, pos.Y, pos.X + size.X, pos.Y + size.Y));   // viewport space: scaled by the transform

            canvas.Concat(skMatrix);

            if (!bitmapShader.TryGetValue(bitmap.Handle, out var shader)) {
                shader = SKShader.CreateBitmap(bitmap, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
                bitmapShader[bitmap.Handle] = shader;
            }
            paint.Shader = shader;
            
            // was: canvas.DrawVertices(SKVertexMode.Triangles, positions, texs, colors, skIndices, paint);
            
            var blendMode = GetBlendMode(cmd.blendState);
            
            var skVertices = SkiaApiNative.CreateCopy(SKVertexMode.Triangles, positions, texs, colors, skIndices);
            // var skVertices = SKVertices.CreateCopy(SKVertexMode.Triangles, positions, texs, colors, skIndices);
            canvas.DrawVertices(skVertices, blendMode, paint);

            canvas.Restore();
        }
        paint.Shader = null;
        canvas.Restore();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SKColor ToSKColor(uint rgbaPacked) {
        return new SKColor(
            (byte)( rgbaPacked         & 0xff),
            (byte)((rgbaPacked >>  8)  & 0xff),
            (byte)((rgbaPacked >> 16)  & 0xff),
            (byte)( rgbaPacked >> 24         ));
    }
    
    private static SKBlendMode GetBlendMode(BlendState blendState) {
        return blendState switch {
            BlendState.Alpha            => SKBlendMode.Modulate,
            BlendState.Opaque           => SKBlendMode.Src,
            BlendState.Additive         => SKBlendMode.Plus,        // Closest SKBlendMode equivalent to SrcAlpha + One.
            BlendState.Multiply         => SKBlendMode.Modulate,
            BlendState.AddColors        => SKBlendMode.Plus,
            BlendState.SubtractColors   => SKBlendMode.Difference,  // Approximation; SkiaSharp has no direct subtract mode.
            _                           => SKBlendMode.Modulate
        };
    }
}