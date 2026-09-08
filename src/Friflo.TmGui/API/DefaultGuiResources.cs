// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Text;
using System.IO;

// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;

public class DefaultGuiResources : IGuiResources
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
}