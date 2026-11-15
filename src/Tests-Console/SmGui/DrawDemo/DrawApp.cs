using Friflo.SmGui;

namespace DrawDemo;


public class DrawApp : IGuiApp
{
    private readonly    DrawAppResources resources;
    private readonly    DrawView         nativeView;
    
    internal DrawApp(SmGuiBackend backend)
    {
        resources   = new DrawAppResources(backend);
        nativeView  = new DrawView(resources);
    }

    public void Dispose()
    {
        resources.Dispose();
    }
    
    public void OnFrame(SmBatch batch,  int width, int height)
    {
        nativeView.RenderView(batch, width, height);
    }
}


/// <summary>
/// Contains resources shared by all <see cref="DrawView"/>'s.
/// </summary>
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