using Friflo.SmGui;

namespace TestConsole;

public sealed class DrawAppResources : IDisposable
{
    internal readonly SmTexture myTexture;
        
    public DrawAppResources (SmGuiBackend backend)
    {
        var assembly = typeof(AppWindow).Assembly;
        
        // create tile texture
        using var stream = assembly.GetManifestResourceStream("Tests-Console.Assets.img.world_tileset.png")!;
        myTexture        = backend.LoadTexture(stream, "world_tileset.png"); 
    }
    
    public void Dispose()
    {
        myTexture.Dispose();
    }
}

public class DrawView
{
    
}