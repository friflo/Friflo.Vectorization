// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.IO;

// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public interface IGuiAssets
{
    TmFont              CreateDefaultFont  (TmGuiBackend backend);
    
    TmImageAsset        LoadImage(Stream stream, TmColorComponents colorComponents);
    TmTrueTypeFontAsset LoadTrueTypeFont(
                            Stream  ttfStream,
                            float   fontSize,
                            int     atlasWidth,
                            int     atlasHeight,
                            byte[]  alphaBitmapTarget, 	// [atlasWidth * atlasHeight]
                            int     firstChar,    		// ASCII 32 to 126
                            int     charCount);
}

public struct TmImageAsset
{
    public int      width;
    public int      height;
    public byte[]   data;
}

public enum TmColorComponents
{
    Default,
    Grey,
    GreyAlpha,
    RedGreenBlue,
    RedGreenBlueAlpha,
}

public struct TmTrueTypeFontAsset
{
    public  Dictionary<char, GlyphInfo> glyphs;
    public  int                         maxY;
}