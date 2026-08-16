// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using StbImageSharp;


// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;

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

public class Font : IDisposable
{
    internal readonly   ImTextureView                       textureView;
    public   readonly   Vector2                             textureSize;
    public   readonly   float                               lineHeight;
    public   readonly   FrozenDictionary<char, GlyphInfo>   glyphs;
    private  readonly   GpuTexture                          fontTexture;
    public   readonly   string                              name;

    public  override    string                              ToString() => name;

    private Font (
        GpuTexture                  fontTexture,
        ImTextureView               textureView,
        Vector2                     textureSize,
        float                       lineHeight,
        Dictionary<char, GlyphInfo> glyphs,
        string                      name)
    {
        this.fontTexture    = fontTexture;
        this.textureView    = textureView;
        this.textureSize    = textureSize;
        this.lineHeight     = lineHeight;
        this.glyphs         = glyphs.ToFrozenDictionary();
        this.name           = name;
    }

    public void Dispose()
    {
        fontTexture.Dispose(); // GpuTexture support multi Dispose()
    }

    public bool TryGetGlyph(char c, out GlyphInfo glyph) => glyphs.TryGetValue(c, out glyph);

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

    public static Font CreateFont(GpuDevice device, ReadOnlySpan<char> fntContent, Stream fontAtlas, string name)
    {
        var glyphs = ReadBmFont(fntContent, out float lineHeight);
        
        var image   = ImageResult.FromStream(fontAtlas, ColorComponents.RedGreenBlueAlpha);
        var height  = image.Height; 
        var width   = image.Width;
        var fontTexture = device.CreateTexture(new GpuTextureDescriptor { label = name,
            size    = [height, width],
            format  = TextureFormat.RGBA8Unorm,
            usage   = TextureUsage.TextureBinding | TextureUsage.CopyDst
        });
        int startX = width  - 3;
        int startY = height - 3;

        // set 3x3 pixel at bottom right to white
        for (int y = startY; y < height; y++)
        {
            for (int x = startX; x < width; x++) {
                int index = (y * width + x) * 4; // RGBA8
                image.Data[index + 0] = 255; // R
                image.Data[index + 1] = 255; // G
                image.Data[index + 2] = 255; // B
                image.Data[index + 3] = 255; // A
            }
        }
        fontTexture.Write(image.Data, bytesPerRow: width * 4, rowsPerImage: height);
        
        // Exact center 3x3 white pixels (width - 2, height - 2)
        var whitePixelUv = new Vector2((width - 1.5f) / width, (height - 1.5f) / height);
        
        var textureView = new ImTextureView(fontTexture.CreateView(), whitePixelUv);
        var textureSize = new Vector2(image.Width, image.Height);
        
        return new Font(fontTexture, textureView, textureSize, lineHeight, glyphs, name);
    }
}
