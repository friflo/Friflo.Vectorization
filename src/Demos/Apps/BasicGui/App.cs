using Friflo.SmGui;
using Friflo.SmGui.Headless;
using Friflo.SmGui.Session;

namespace Demos.BasicGui;


public sealed class BasicGuiApp : SmGuiApp
{
    private readonly    GuiDemoView     nativeView;
    private readonly    DemoResources   resources;

    public BasicGuiApp(SmGuiBackend backend)
    {
        resources   = new DemoResources(backend);

        // Set session loop for remote browser GUIs
        SessionLoop = SmSessionLoop.Create(new LoopOptions {
            Backend     = backend,
            ViewFactory = session => new GuiDemoView(resources, session)
        });
        nativeView = new GuiDemoView(resources, SessionLoop.CreateLocalSession(backend));
    }

    public override void Dispose()
    {
        SessionLoop?.Dispose();
        resources.Dispose();
    }
    
    public override void RenderApp(SmBatch batch,  int width, int height)
    {
        SessionLoop?.IterateSessions();
        
        nativeView.RenderGui(batch, width, height);
    }
}
