using Friflo.SmGui;

namespace DrawDemo;


public sealed class DemoDrawApp : SmGuiApp
{
    private readonly    DrawAppResources resources;
    private readonly    DrawView         nativeView;
    
    internal DemoDrawApp(SmGuiBackend backend)
    {
        clearValue  = 0x1A1A1AFF;
        resources   = new DrawAppResources(backend);
        nativeView  = new DrawView(resources);
    }

    public override void Dispose()
    {
        resources.Dispose();
    }
    
    public override void RenderApp(SmBatch batch,  int width, int height)
    {
        nativeView.RenderView(batch, width, height);
    }
}


/// <summary> Contains resources shared by all <see cref="DrawView"/> instances. </summary>
/// <remarks> Organizing resources in a separate class is optional but brings clarity to resource management.</remarks>
public sealed class DrawAppResources : IDisposable
{
    internal readonly SmTexture myTexture;
        
    public DrawAppResources (SmGuiBackend backend)
    {
        var assembly = typeof(DemoDrawApp).Assembly;
        
        // create tile texture
        using var stream = assembly.GetManifestResourceStream("Tests-Console.Assets.img.world_tileset.png")!;
        myTexture        = backend.LoadTexture(stream, "world_tileset.png");
    }
    
    public void Dispose()
    {
        myTexture.Dispose();
    }
}