// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using StbImageSharp;
using StbTrueTypeSharp;

// ReSharper disable InconsistentNaming
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;

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

public sealed class Font : IDisposable
{
    internal readonly   ImTexture                           texture;
    public   readonly   Vector2                             textureSize;
    public   readonly   float                               lineHeight;
    public   readonly   FrozenDictionary<char, GlyphInfo>   glyphs;
    public   readonly   string                              name;
    public   readonly   int                                 maxY;
    private  readonly   bool                                disposable;
    
    public  override    string                              ToString()  => name;

    private Font (
        ImTexture               	texture,
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
        if (texture.obj is IDisposable disposableTexture) {
            disposableTexture.Dispose();
        }
    }
    
    internal void DisposeInternal()
    {
        if (texture.obj is IDisposable disposableTexture) {
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

    internal static Font CreateBMFont(ImGuiBackend backend, ReadOnlySpan<char> fntContent, Stream fontAtlas, string name, bool disposable)
    {
        var glyphs = ReadBmFont(fntContent, out float lineHeight);
        
        var image   = ImageResult.FromStream(fontAtlas, ColorComponents.RedGreenBlueAlpha);
        var width   = image.Width;
        var height  = image.Height; 
        AssertTextureDimension(width, height);
        
        var whitePixelUv = SetWhitePixel(width, height, image.Data);

        var fontTexture = backend.CreateTexture(name, width, height, image.Data);
        
        var imTexture   = new ImTexture(fontTexture, whitePixelUv);
        var textureSize = new Vector2(image.Width, image.Height);
        
        return new Font(imTexture, textureSize, lineHeight, glyphs, name, -1, disposable);
    }
#endregion



#region TTF
    private static unsafe Dictionary<char, GlyphInfo> ReadTtf(
        byte[]  ttfData,
        float   fontSize,
        int     atlasWidth,
        int     atlasHeight,
        byte[]  alphaBitmapTarget, 	// [atlasWidth * atlasHeight]
        int     firstChar,    		// ASCII 32 to 126
        int     charCount,
        out int maxY)
    {
        var bakedChars = new StbTrueType.stbtt_bakedchar[charCount];

        var success = StbTrueType.stbtt_BakeFontBitmap(
            ttfData, 0,
            fontSize,
            alphaBitmapTarget,
            atlasWidth, atlasHeight,
            firstChar, charCount,
            bakedChars
        );

        if (!success) {
            throw new InvalidOperationException($"Atlas ({atlasWidth}x{atlasHeight}) too small for fontSize {fontSize}.");
        }

        // retrieve ascent (Baseline-distance from top edge)
        float ascent = fontSize * 0.75f; // Standard-Fallback
        var fontInfo = new StbTrueType.stbtt_fontinfo();
        
        fixed(byte* ttfDataPt = ttfData) {
            if (StbTrueType.stbtt_InitFont(fontInfo, ttfDataPt, 0) != 0) {
                int rawAscent;
                int rawDescent;
                int rawLineGap;
                StbTrueType.stbtt_GetFontVMetrics(fontInfo, &rawAscent, &rawDescent, &rawLineGap);
                float scale = StbTrueType.stbtt_ScaleForPixelHeight(fontInfo, fontSize);
                ascent = MathF.Round(rawAscent * scale);
            }
        }

        var glyphs = new Dictionary<char, GlyphInfo>(charCount);
        maxY = 0;

        for (int i = 0; i < charCount; i++)
        {
            var baked = bakedChars[i];
            char c = (char)(firstChar + i);
            if (maxY < baked.y1) maxY = baked.y1; 

            glyphs[c] = new GlyphInfo {
                sourcePos  = new Vector2(baked.x0, baked.y0),
                sourceSize = new Vector2(baked.x1 - baked.x0, baked.y1 - baked.y0),
                // Bake ascent directly into yoff -> top-left ready!
                offset     = new Vector2(baked.xoff, baked.yoff + ascent),
                advance    = baked.xadvance
            };
        }
        return glyphs;
    }
    
    internal static Font CreateTtfFont(
        ImGuiBackend backend,
        Stream      ttfStream,
        float       fontSize,
        int         width,
        int         height,
        int         firstChar,
        int         charCount,
        string      name,
        bool        disposable)
    {
        AssertTextureDimension(width, height);
        
        byte[] ttfData;
        if (ttfStream is MemoryStream typedMemoryStream) {
            ttfData = typedMemoryStream.ToArray();
        } else {
            using var ms = new MemoryStream();
            ttfStream.CopyTo(ms);
            ttfData = ms.ToArray();
        }
        var alphaBitmapTarget = new byte[width * height];
        var glyphs = ReadTtf(ttfData, fontSize, width, height, alphaBitmapTarget, firstChar, charCount, out var maxY);
        
        
        
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
        
        var imTexture   = new ImTexture(fontTexture, whitePixelUv);
        var textureSize = new Vector2(width, height);
        
        return new Font(imTexture, textureSize, fontSize, glyphs, name, maxY, disposable);
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
    
    private static void AssertTextureDimension(int width, int height)
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
