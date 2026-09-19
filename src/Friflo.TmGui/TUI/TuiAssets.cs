using System.IO;
using Friflo.TmGui.Headless;
using StbImageSharp;


namespace Friflo.TmGui.TUI;

public class TuiAssets : IGuiAssets
{
    public TmFont CreateDefaultFont(TmGuiBackend backend)
    {
        return HeadlessAssets.CreateHeadlessFont();
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

    public TmTrueTypeFontAsset LoadTrueTypeFont(Stream ttfStream, float fontSize, int atlasWidth, int atlasHeight, byte[] alphaBitmapTarget, int firstChar, int charCount)
    {
        throw new System.NotImplementedException();
    }
}