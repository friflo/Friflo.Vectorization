// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.IO;

// ReSharper disable once CheckNamespace
namespace Friflo.SmGui;


public interface IGuiAssets
{
    SmFont              CreateDefaultFont  (SmGuiBackend backend);
    
    SmImageAsset        LoadImage(Stream stream, SmColorComponents colorComponents);
    Stream              CreatePng(SmImageAsset asset);
    SmTrueTypeFontAsset LoadTrueTypeFont(
                            Stream  ttfStream,
                            float   fontSize,
                            int     atlasWidth,
                            int     atlasHeight,
                            byte[]  alphaBitmapTarget, 	// [atlasWidth * atlasHeight]
                            int     firstChar,    		// ASCII 32 to 126
                            int     charCount);
}

public struct SmImageAsset
{
    public int      width;
    public int      height;
    public byte[]   data;
    public string   name;
}

public enum SmColorComponents
{
    Default,
    Grey,
    GreyAlpha,
    RedGreenBlue,
    RedGreenBlueAlpha,
}

public struct SmTrueTypeFontAsset
{
    public  Dictionary<int, GlyphInfo>  glyphs;
    public  int                         maxY;
}