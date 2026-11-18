using Friflo.SmGui;

namespace Demos.BasicGui;


public sealed class BasicGuiApp : SmGuiApp
{
    private  readonly   BasicGuiView    nativeView;
    // --- resources
    internal readonly   SmTexture       myTexture;
    
    public BasicGuiApp(SmGuiBackend backend)
    {
        clearValue  = 0x1A1A1AFF;
        nativeView  = new BasicGuiView(this);
        
        // --- resources
        var assembly = typeof(BasicGuiApp).Assembly;
        
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
