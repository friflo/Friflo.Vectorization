using Friflo.SmGui;

namespace DrawExample;


public sealed class DrawDemo : SmGuiApp
{
    private  readonly   DrawDemoView    nativeView;
    // --- resources
    internal readonly   SmTexture       myTexture;
    
    internal DrawDemo(SmGuiBackend backend)
    {
        clearValue  = 0x1A1A1AFF;
        nativeView  = new DrawDemoView(this);
        
        // --- resources
        var assembly = typeof(DrawDemo).Assembly;
        
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
