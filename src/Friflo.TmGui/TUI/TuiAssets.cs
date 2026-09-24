using System;
using System.IO;
using Friflo.TmGui.Headless;


namespace Friflo.TmGui.TUI;

public class TuiAssets : IGuiAssets
{
    public TmFont CreateDefaultFont(TmGuiBackend backend)
    {
        return HeadlessAssets.CreateHeadlessFont();
    }

    public TmImageAsset LoadImage(Stream stream, TmColorComponents colorComponents)
    {
        throw Requires_Friflo_TmGui_Assets_Exception(nameof(LoadImage));
    }

    public TmTrueTypeFontAsset LoadTrueTypeFont(Stream ttfStream, float fontSize, int atlasWidth, int atlasHeight, byte[] alphaBitmapTarget, int firstChar, int charCount)
    {
        throw Requires_Friflo_TmGui_Assets_Exception(nameof(LoadTrueTypeFont));
    }
    
    private static InvalidOperationException Requires_Friflo_TmGui_Assets_Exception(string symbol)
    {
        return new InvalidOperationException($"{symbol}() requires IGuiAssets from package: Friflo.TmGui.Assets - instance: new DefaultGuiAssets()");
    }
}