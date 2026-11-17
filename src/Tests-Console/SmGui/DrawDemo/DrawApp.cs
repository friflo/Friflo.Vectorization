using Friflo.SmGui;

namespace DrawDemo;


public sealed class DemoDrawApp : SmGuiApp
{
    private  readonly   DrawView    nativeView;
    // --- resources
    internal readonly   SmTexture   myTexture;
    
    internal DemoDrawApp(SmGuiBackend backend)
    {
        clearValue  = 0x1A1A1AFF;
        nativeView  = new DrawView(this);
        
        // --- resources
        var assembly = typeof(DemoDrawApp).Assembly;
        
        using var stream = assembly.GetManifestResourceStream("Tests-Console.Assets.img.world_tileset.png")!;
        myTexture        = backend.LoadTexture(stream, "world_tileset.png");
    }

    public override void Dispose()
    {
        myTexture.Dispose();
    }
    
    public override void RenderApp(SmBatch batch,  int width, int height)
    {
        nativeView.RenderView(batch, width, height);
    }
}
