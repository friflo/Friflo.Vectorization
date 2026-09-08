// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.IO;

// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;

public interface IGuiAssets
{
    TmFont          CreateDefaultFont  (TmGuiBackend backend);
    TmFont          CreateMonocraftFont(TmGuiBackend backend, float fontSize, int width, int height, int firstChar, int charCount, string name);
    
    TmImageAsset    LoadImage(Stream stream, TmColorComponents colorComponents);
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
