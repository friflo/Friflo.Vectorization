using Friflo.SmGui;

namespace DrawDemo;


public class DrawApp : IGuiApp
{
    private readonly    DrawAppResources resources;
    private readonly    DrawView         nativeView;
    
    internal DrawApp(SmGuiBackend backend)
    {
        resources       = new DrawAppResources(backend);
        nativeView      = new DrawView(resources);
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