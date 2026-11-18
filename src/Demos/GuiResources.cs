using System;
using System.Diagnostics;
using Friflo.SmGui;

namespace Demos;

public class GuiResources : IDisposable
{
    internal readonly   SmTexture   worldTileset;
    internal readonly   SmFont      monocraftFont;
    
    public GuiResources(SmGuiBackend backend) {
        var assembly = typeof(GuiResources).Assembly;
                
        using var stream    = assembly.GetManifestResourceStream("Demos.Assets.world_tileset.png")!;
        worldTileset        = backend.LoadTexture(stream, "world_tileset.png");
        
        using var monoTtf   = assembly.GetManifestResourceStream("Demos.Assets.Monocraft.ttf")!;  
        monocraftFont       = backend.CreateTtfFont(monoTtf, 48, 256, 256, 32, 95, "Monocraft");
        Debug.Assert(monocraftFont.maxY == 244);
    }

    public void Dispose()
    {
        monocraftFont.Dispose();
        worldTileset.Dispose();
    }
}