using Friflo.SmGui;

namespace Demos.BasicDraw;


public sealed class BasicDrawApp : SmGuiApp
{
    private  readonly   DrawDemoView    nativeView;
    // --- resources
    internal readonly   SmTexture       myTexture;
    
    public BasicDrawApp(SmGuiBackend backend)
    {
        clearValue  = 0x1A1A1AFF;
        nativeView  = new DrawDemoView(this);
        
        // --- resources
        var assembly = typeof(BasicDrawApp).Assembly;
        
        using var stream = assembly.GetManifestResourceStream("Demos.Assets.world_tileset.png")!;
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
