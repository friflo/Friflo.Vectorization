// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable MergeIntoPattern
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public readonly ref partial struct TmDraw
{

    /// <summary>
    /// Draws a sprite using normal 0..1 UV coordinates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(in TmTexture texture, Vector2 position, Vector2 size)
    {
        DrawSprite(texture, position, size, default, new Vector2(1f, 1f), Color32.White);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(in TmTexture texture, Vector2 position, Vector2 size, Color32 color)
    {
        DrawSprite(texture, position, size, default, new Vector2(1f, 1f), color);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(in TmTexture texture, Vector2 position, Vector2 size, Vector2 uvMin, Vector2 uvMax)
    {
        DrawSprite(texture, position, size, uvMin, uvMax, Color32.White);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(in TmTexture texture, Vector2 position, Vector2 size, Vector2 uvMin, Vector2 uvMax, Color32 color)
    {
        var bat = batch;
        if (bat.vertexCount + 4 > bat.vertexBuffer.Length || bat.currentTexture != texture) {
            bat.Flush();
            bat.currentTexture = texture;
        }
        float x1 = position.X;
        float y1 = position.Y;
        float x2 = position.X + size.X;
        float y2 = position.Y + size.Y;

        var packed   = color.Packed;
        ref var quad = ref AddQuad();
        quad[0] = new Vertex2D(new Vector2(x1, y1), uvMin,                         packed);
        quad[1] = new Vertex2D(new Vector2(x2, y1), new Vector2(uvMax.X, uvMin.Y), packed);
        quad[2] = new Vertex2D(new Vector2(x2, y2), uvMax,                         packed);
        quad[3] = new Vertex2D(new Vector2(x1, y2), new Vector2(uvMin.X, uvMax.Y), packed);
    }


    /// <summary>
    /// Draws a sub-region (source rect in pixels) from a texture/spritesheet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSpriteRegion(in TmTexture texture, Vector2 position, Vector2 size, Vector2 sourceRectPos, Vector2 sourceRectSize, Vector2 textureSize)
    {
        DrawSpriteRegion(texture, position, size, sourceRectPos, sourceRectSize, textureSize, Color32.White);
    }

    /// <summary>
    /// Draws a sub-region (source rect in pixels) from a texture/spritesheet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSpriteRegion(in TmTexture texture, Vector2 position, Vector2 size, Vector2 sourceRectPos, Vector2 sourceRectSize, Vector2 textureSize, Color32 color)
    {
        Vector2 uvMin = sourceRectPos / textureSize;
        Vector2 uvMax = (sourceRectPos + sourceRectSize) / textureSize;
        DrawSprite(texture, position, size, uvMin, uvMax, color);
    }
    
    /// <summary>
    /// Draws a rotated sprite with pivot (0..1 normalized).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSpriteRotated(in TmTexture texture, Vector2 position, Vector2 size, float rotation, Vector2 pivot, Color32? color = null)
    {
        DrawSpriteRotated(texture, position, size, rotation, pivot, default, new Vector2(1f, 1f), color);
    }

    /// <summary>
    /// Draws a rotated sub-region from a texture with pivot (0..1 normalized).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSpriteRegionRotated(in TmTexture texture, Vector2 position, Vector2 size, float rotation, Vector2 pivot, Vector2 sourceRectPos, Vector2 sourceRectSize, Vector2 textureSize, Color32? color = null)
    {
        Vector2 uvMin = sourceRectPos / textureSize;
        Vector2 uvMax = (sourceRectPos + sourceRectSize) / textureSize;
        DrawSpriteRotated(texture, position, size, rotation, pivot, uvMin, uvMax, color);
    }
    
    /// <summary>
    /// Draws a rotated quad transform around a normalized pivot (0..1).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSpriteRotated(in TmTexture texture, Vector2 position, Vector2 size, float rotation, Vector2 pivot, Vector2 uvMin, Vector2 uvMax, Color32? color = null)
    {
        var colorVal = color ?? Color32.White;
        if (rotation == 0f) {
            DrawSprite(texture, position - (pivot * size), size, uvMin, uvMax, colorVal);
            return;
        }
        var bat = batch;
        if (bat.vertexCount + 4 > bat.vertexBuffer.Length || bat.currentTexture != texture) {
            bat.Flush();
            bat.currentTexture = texture;
        }
        float cos = MathF.Cos(rotation);
        float sin = MathF.Sin(rotation);

        // Local offsets relative to pivot
        float l = -pivot.X * size.X;
        float r = (1f - pivot.X) * size.X;
        float t = -pivot.Y * size.Y;
        float b = (1f - pivot.Y) * size.Y;

        uint packed  = colorVal.Packed;
        ref var quad = ref AddQuad();
        quad[0] = new Vertex2D(new Vector2(position.X + l * cos - t * sin, position.Y + l * sin + t * cos), uvMin,                         packed);
        quad[1] = new Vertex2D(new Vector2(position.X + r * cos - t * sin, position.Y + r * sin + t * cos), new Vector2(uvMax.X, uvMin.Y), packed);
        quad[2] = new Vertex2D(new Vector2(position.X + r * cos - b * sin, position.Y + r * sin + b * cos), uvMax,                         packed);
        quad[3] = new Vertex2D(new Vector2(position.X + l * cos - b * sin, position.Y + l * sin + b * cos), new Vector2(uvMin.X, uvMax.Y), packed);
    }

    /// <summary>
    /// Draws a 9-slice sprite using the full texture where borders and center are tiled (repeated).
    /// borderThickness: (Left, Top, Right, Bottom) in pixels.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Draw9SliceTiled(
        in TmTexture	texture,
        Vector2         position, 
        Vector2         size, 
        Vector4         borderThickness, 
        Vector2         textureSize, 
        Color32?        color = null)
    {
        Draw9SliceTiled(texture, position, size, Vector2.Zero, textureSize, textureSize, borderThickness, color);
    }

    /// <summary>
    /// Draws a 9-slice sprite from a sub-region (Spritesheet/Atlas) where borders and center are tiled (repeated).
    /// borderThickness: (Left, Top, Right, Bottom) in pixels.
    /// </summary>
    public void Draw9SliceTiled(
    	in TmTexture 	texture,
        Vector2 	    position,
        Vector2 	    size,
        Vector2 	    sourceRectPos,
        Vector2 	    sourceRectSize,
        Vector2 	    textureSize,
        Vector4 	    borderThickness,
        Color32? 	    color = null)
    {
        var color32 = color ?? Color32.White;

        float L = borderThickness.X;
        float T = borderThickness.Y;
        float R = borderThickness.Z;
        float B = borderThickness.W;

        float srcInnerW = sourceRectSize.X - L - R;
        float srcInnerH = sourceRectSize.Y - T - B;

        float destInnerW = size.X - L - R;
        float destInnerH = size.Y - T - B;

        // prevent division by 0 of invalid values
        if (srcInnerW <= 0f || srcInnerH <= 0f) return;

        // UV-coordinates of 3x3 grid
        Vector2 u0 = sourceRectPos / textureSize;
        Vector2 u1 = (sourceRectPos + new Vector2(L, T)) / textureSize;
        Vector2 u2 = (sourceRectPos + sourceRectSize - new Vector2(R, B)) / textureSize;
        Vector2 u3 = (sourceRectPos + sourceRectSize) / textureSize;

        // --- 4 corners (fixed size) ---
        DrawSprite(texture, position, new Vector2(L, T), u0, u1, color32);
        DrawSprite(texture, new Vector2(position.X + size.X - R, position.Y), new Vector2(R, T), new Vector2(u2.X, u0.Y), new Vector2(u3.X, u1.Y), color32);
        DrawSprite(texture, new Vector2(position.X, position.Y + size.Y - B), new Vector2(L, B), new Vector2(u0.X, u2.Y), new Vector2(u1.X, u3.Y), color32);
        DrawSprite(texture, new Vector2(position.X + size.X - R, position.Y + size.Y - B), new Vector2(R, B), u2, u3, color32);

        // --- top bottom border (Horizontal tiled) ---
        for (float x = 0; x < destInnerW; x += srcInnerW)
        {
            float drawW = MathF.Min(srcInnerW, destInnerW - x);
            float uMaxX = u1.X + (u2.X - u1.X) * (drawW / srcInnerW);

            DrawSprite(texture, new Vector2(position.X + L + x, position.Y), new Vector2(drawW, T), new Vector2(u1.X, u0.Y), new Vector2(uMaxX, u1.Y), color32);
            DrawSprite(texture, new Vector2(position.X + L + x, position.Y + size.Y - B), new Vector2(drawW, B), new Vector2(u1.X, u2.Y), new Vector2(uMaxX, u3.Y), color32);
        }

        // --- left / right border (vertical tiled) ---
        for (float y = 0; y < destInnerH; y += srcInnerH)
        {
            float drawH = MathF.Min(srcInnerH, destInnerH - y);
            float uMaxY = u1.Y + (u2.Y - u1.Y) * (drawH / srcInnerH);

            DrawSprite(texture, new Vector2(position.X, position.Y + T + y), new Vector2(L, drawH), new Vector2(u0.X, u1.Y), new Vector2(u1.X, uMaxY), color32);
            DrawSprite(texture, new Vector2(position.X + size.X - R, position.Y + T + y), new Vector2(R, drawH), new Vector2(u2.X, u1.Y), new Vector2(u3.X, uMaxY), color32);
        }

        // --- inner area (2D-grid tiled) ---
        for (float x = 0; x < destInnerW; x += srcInnerW)
        {
            float drawW = MathF.Min(srcInnerW, destInnerW - x);
            float uMaxX = u1.X + (u2.X - u1.X) * (drawW / srcInnerW);

            for (float y = 0; y < destInnerH; y += srcInnerH)
            {
                float drawH = MathF.Min(srcInnerH, destInnerH - y);
                float uMaxY = u1.Y + (u2.Y - u1.Y) * (drawH / srcInnerH);

                DrawSprite(texture, new Vector2(position.X + L + x, position.Y + T + y), new Vector2(drawW, drawH), u1, new Vector2(uMaxX, uMaxY), color32);
            }
        }
    }
}

