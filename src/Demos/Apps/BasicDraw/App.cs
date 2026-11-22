using Friflo.SmGui;
using Friflo.SmGui.Headless;
using Friflo.SmGui.Session;

namespace Demos.BasicDraw;


public sealed class BasicDrawApp : SmGuiApp
{
    private  readonly   DrawDemoView    nativeView;
    private  readonly   DemoResources   resources;

    
    public BasicDrawApp(SmGuiBackend backend)
    {
        clearValue  = 0x1A1A1AFF;
        resources   = new DemoResources(backend);
        
        // Set session loop for remote browser GUIs
        SessionLoop = SmSessionLoop.Create(new LoopOptions {
            Backend     = new CpuBackend("SmGui - Remote Test", backend),
            ViewFactory = session => new DrawDemoView(resources, session)
        });
        nativeView = new DrawDemoView(resources, SessionLoop.CreateLocalSession(backend));
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
