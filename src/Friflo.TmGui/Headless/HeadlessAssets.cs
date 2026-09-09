// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Numerics;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable EmptyConstructor
// ReSharper disable RedundantOverriddenMember
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.Headless;


internal class HeadlessAssets : IGuiAssets
{
    public TmFont CreateDefaultFont(TmGuiBackend backend)
    {
         // Simulate monospace font
        var glyph = new GlyphInfo {
            sourceSize = new Vector2(20.0f, 32.0f),
            offset     = new Vector2( 2.0f,  3.0f),
            advance    = 24.0f
        };
        var glyphs = new Dictionary<char, GlyphInfo>();
        for (int n = 0; n <= 190; n++)
        {
            int col = n % 20;
            int row = n / 20;
            glyph.sourcePos = new Vector2(col * 24.0f, row * 35.0f);
            glyphs.Add((char)n, glyph);
        }
        var fontTexture = new HeadlessTexture("Headless Font Texture", 512, 512, default);
        var texture     = new TmTexture(fontTexture, 0, default);   // use texture with simulated white UV pixel for testing
        var textureSize = new Vector2(fontTexture.width, fontTexture.height);
        
        return new TmFont(texture, textureSize, 47, glyphs, "Headless Font", -1, false);
    }
    
    public TmImageAsset LoadImage(Stream stream, TmColorComponents colorComponents)
    {
        return default;
    }
    
    public TmTrueTypeFontAsset LoadTrueTypeFont(
        Stream  ttfStream,
        float   fontSize,
        int     atlasWidth,
        int     atlasHeight,
        byte[]  alphaBitmapTarget, 	// [atlasWidth * atlasHeight]
        int     firstChar,    		// ASCII 32 to 126
        int     charCount)
    {
        return default;
    }
}
