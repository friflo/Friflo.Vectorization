// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Numerics;
using StbImageSharp;
using StbTrueTypeSharp;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public class DefaultGuiAssets : IGuiAssets
{
    public TmFont CreateDefaultFont(TmGuiBackend backend)
    {
        using var fontAtlas = typeof(TmGuiBackend).Assembly.GetManifestResourceStream("Friflo.TmGui.fonts.arial-48-latin_0.png");
        using var fntFile   = typeof(TmGuiBackend).Assembly.GetManifestResourceStream("Friflo.TmGui.fonts.arial-48-latin.fnt");
        using var reader    = new StreamReader(fntFile!, Encoding.UTF8);
        var fntContent      = reader.ReadToEnd();
        
        return TmFont.CreateBMFont(backend, fntContent, fontAtlas!, "Default Font", false);
    }
    
    /// <summary> E.g. <c>device.CreateMonocraftFont(48, 256, 256, 32, 95, "Monocraft");</c> </summary>
    public TmFont CreateMonocraftFont(TmGuiBackend backend, float fontSize, int width, int height, int firstChar, int charCount, string name)
    {
        using var ttfFont = typeof(TmGuiBackend).Assembly.GetManifestResourceStream("Friflo.TmGui.fonts.Monocraft.ttf")!;
        
        return TmFont.CreateTtfFont(backend, ttfFont, fontSize, width, height, firstChar, charCount, name, true);
    }
    
    public TmImageAsset LoadImage(Stream stream, TmColorComponents colorComponents)
    {
        var result = ImageResult.FromStream(stream, (ColorComponents)colorComponents);
        return new TmImageAsset {
            width   = result.Width,
            height  = result.Height,
            data    = result.Data,
        };  
    }
    
    
    public unsafe TmTrueTypeFontAsset LoadTrueTypeFontAsset(
        Stream  ttfStream,
        float   fontSize,
        int     atlasWidth,
        int     atlasHeight,
        byte[]  alphaBitmapTarget, 	// [atlasWidth * atlasHeight]
        int     firstChar,    		// ASCII 32 to 126
        int     charCount)
    {
        byte[] ttfData;
        if (ttfStream is MemoryStream typedMemoryStream) {
            ttfData = typedMemoryStream.ToArray();
        } else {
            using var ms = new MemoryStream();
            ttfStream.CopyTo(ms);
            ttfData = ms.ToArray();
        }
        
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
        int maxY = 0;

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
        return new TmTrueTypeFontAsset {
            glyphs  = glyphs,
            maxY    = maxY
        };
    }
}

