// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
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
    public  readonly    GpuTextureView              textureView;
    public  readonly    Vector2                     textureSize;
    public  readonly    float                       lineHeight;
    private readonly    Dictionary<char, GlyphInfo> glyphs;
    private readonly    GpuTexture                  fontTexture;
    public  readonly    string                      name;

    public  override    string                      ToString() => name;

    private Font (GpuTexture fontTexture, GpuTextureView textureView, Vector2 textureSize, float lineHeight, Dictionary<char, GlyphInfo> glyphs, string name)
    {
        this.fontTexture    = fontTexture;
        this.textureView    = textureView;
        this.textureSize    = textureSize;
        this.lineHeight     = lineHeight;
        this.glyphs         = glyphs;
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
    private static void ReadBmFont(ReadOnlySpan<char> fntContent, Dictionary<char, GlyphInfo> glyphs, out float lineHeight)
    {
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
        var glyphs = new Dictionary<char, GlyphInfo>();
        ReadBmFont(fntContent, glyphs, out float lineHeight);
        
        var image   = ImageResult.FromStream(fontAtlas, ColorComponents.RedGreenBlueAlpha);
        var fontTexture = device.CreateTexture(new GpuTextureDescriptor { label = name,
            size    = [image.Width, image.Height],
            format  = TextureFormat.RGBA8Unorm,
            usage   = TextureUsage.TextureBinding | TextureUsage.CopyDst
        });
        fontTexture.Write(image.Data, bytesPerRow: image.Width * 4, rowsPerImage: image.Height);
        
        var textureView = fontTexture.CreateView();
        var textureSize = new Vector2(image.Width, image.Height);
        
        return new Font(fontTexture, textureView, textureSize, lineHeight, glyphs, name);
    }
}
