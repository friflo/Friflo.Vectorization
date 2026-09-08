// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;


// ReSharper disable InconsistentNaming
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;

public enum TextAlignment
{
    Left,
    Center,
    Right
}

public enum VerticalAlignment
{
    Top,
    Middle,
    Bottom
}


public struct GlyphInfo
{
    public Vector2  sourcePos;  // Pixel position in the atlas (X, Y)
    public Vector2  sourceSize; // Pixel dimensions in the atlas (Width, Height)
    public Vector2  offset;     // Rendering offset relative to the cursor (bearingX, bearingY)
    public float    advance;    // Horizontal advance to the next character
}

public sealed class TmFont : IDisposable
{
    internal readonly   TmTexture                           texture;
    public   readonly   Vector2                             textureSize;
    public   readonly   float                               lineHeight;
    public   readonly   FrozenDictionary<char, GlyphInfo>   glyphs;
    public   readonly   string                              name;
    public   readonly   int                                 maxY;
    private  readonly   bool                                disposable;
    
    public  override    string                              ToString()  => name;

    internal TmFont (
        in TmTexture                texture,
        Vector2                     textureSize,
        float                       lineHeight,
        Dictionary<char, GlyphInfo> glyphs,
        string                      name,
        int                         maxY,
        bool                        disposable)
    {
        this.texture        = texture;
        this.textureSize    = textureSize;
        this.lineHeight     = lineHeight;
        this.glyphs         = glyphs.ToFrozenDictionary();
        this.name           = name;
        this.maxY           = maxY;
        this.disposable     = disposable;
    }

    public void Dispose()
    {
        if (!disposable) {
            return;
        }
        if (texture.native is IDisposable disposableTexture) {
            disposableTexture.Dispose();
        }
    }
    
    internal void DisposeInternal()
    {
        if (texture.native is IDisposable disposableTexture) {
            disposableTexture.Dispose();
        }
    }


    public bool TryGetGlyph(char c, out GlyphInfo glyph) => glyphs.TryGetValue(c, out glyph);

    
#region BM Font
    /// <summary>
    /// Parses a BMFont (.fnt text format) string and pairs it with the atlas texture.
    /// </summary>
    private static Dictionary<char, GlyphInfo> ReadBmFont(ReadOnlySpan<char> fntContent, out float lineHeight)
    {
        var glyphs = new Dictionary<char, GlyphInfo>();
        lineHeight = 0;
        foreach (var lineSpan in fntContent.EnumerateLines())
        {
            var line = lineSpan.Trim();
            if (line.StartsWith("common"))
            {
                lineHeight = ParseValue(line, "lineHeight=");
            }
            else if (line.StartsWith("char") && line.Length > 4 && char.IsWhiteSpace(line[4]))
            {
                char id = (char)ParseValue(line, "id=");
                var glyph = new GlyphInfo
                {
                    sourcePos  = new Vector2(ParseValue(line, "x="), ParseValue(line, "y=")),
                    sourceSize = new Vector2(ParseValue(line, "width="), ParseValue(line, "height=")),
                    offset     = new Vector2(ParseValue(line, "xoffset="), ParseValue(line, "yoffset=")),
                    advance    = ParseValue(line, "xadvance=")
                };
                glyphs[id] = glyph;
            }
        }
        return glyphs;
    }

    private static float ParseValue(ReadOnlySpan<char> line, ReadOnlySpan<char> key)
    {
        int idx = line.IndexOf(key);
        if (idx == -1) return 0f;
        var valueSpan = line[(idx + key.Length)..];
        int spaceIdx = valueSpan.IndexOf(' ');
        if (spaceIdx != -1) valueSpan = valueSpan[..spaceIdx];
        return float.TryParse(valueSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ? result : 0f;
    }

    internal static TmFont CreateBMFont(TmGuiBackend backend, ReadOnlySpan<char> fntContent, TmImageAsset image, string name, bool disposable)
    {
        var glyphs  = ReadBmFont(fntContent, out float lineHeight);
        var width   = image.width;
        var height  = image.height; 
        AssertTextureDimension(width, height);
        
        var whitePixelUv = SetWhitePixel(width, height, image.data);

        var fontTexture = backend.CreateTexture(name, width, height, image.data);
        
        var imTexture   = new TmTexture(fontTexture, whitePixelUv);
        var textureSize = new Vector2(image.width, image.height);
        
        return new TmFont(imTexture, textureSize, lineHeight, glyphs, name, -1, disposable);
    }
#endregion



#region TTF
    internal static TmFont CreateTtfFont(
        TmGuiBackend        backend,
        TmTrueTypeFontAsset fontAsset,
        byte[]              alphaBitmapTarget,
        float               fontSize,
        int                 width,
        int                 height,
        string              name,
        bool                disposable)
    {
        var rgba32 = new byte[width * height * 4];
        
        for (int n = 0; n < alphaBitmapTarget.Length; n++) {
            int offset = n * 4;
            rgba32[offset + 0] = 255;   // R (white, font color for vertex)
            rgba32[offset + 1] = 255;   // G
            rgba32[offset + 2] = 255;   // B
            rgba32[offset + 3] = alphaBitmapTarget[n];
        }
        var whitePixelUv = SetWhitePixel(width, height, rgba32);

        var fontTexture = backend.CreateTexture(name, width, height, rgba32);
        
        var imTexture   = new TmTexture(fontTexture, whitePixelUv);
        var textureSize = new Vector2(width, height);
        
        return new TmFont(imTexture, textureSize, fontSize, fontAsset.glyphs, name, fontAsset.maxY, disposable);
    }
#endregion
    

    private static Vector2 SetWhitePixel(int width, int height, byte[] data)
    {
        int startX = width  - 4;
        int startY = height - 4;

        // set 4x4 pixel at bottom right to white
        for (int y = startY; y < height; y++)
        {
            for (int x = startX; x < width; x++) {
                int index = (y * width + x) * 4; // RGBA8
                data[index + 0] = 255; // R
                data[index + 1] = 255; // G
                data[index + 2] = 255; // B
                data[index + 3] = 255; // A
            }
        }
        // Exact center 4x4 white pixels
        return new Vector2((width - 2f) / width, (height - 2f) / height);
    }
    
    internal static void AssertTextureDimension(int width, int height)
    {
        // assert: power of two for width & height
        if ((width & (width - 1)) != 0 || (height & (height - 1)) != 0) {
            throw new ArgumentException($"Font atlas dimensions ({width}x{height}) must be a power of two (e.g., 256, 512, 1024).");
        }

        // assert: WebGPU 256-Byte Alignment Check for RGBA8 (4 Bytes/Pixel -> width must be dividable by 64)
        if (width % 64 != 0) {
            throw new ArgumentException($"Font atlas width ({width}) must be a multiple of 64 to fulfill WebGPU row alignment rules.");
        }
    }
}
