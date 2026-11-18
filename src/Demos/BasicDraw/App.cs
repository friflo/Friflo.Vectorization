using Friflo.SmGui;

namespace Demos.BasicDraw;


public sealed class BasicDrawApp : SmGuiApp
{
    private  readonly   DrawDemoView    nativeView;
    private  readonly   GuiResources    resources;

    
    public BasicDrawApp(SmGuiBackend backend)
    {
        clearValue  = 0x1A1A1AFF;
        resources   = new GuiResources(backend);
        nativeView  = new DrawDemoView(resources);
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
