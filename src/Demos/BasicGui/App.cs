using Friflo.SmGui;
using Friflo.SmGui.Headless;
using Friflo.SmGui.Session;

namespace Demos.BasicGui;


public sealed class BasicGuiApp : SmGuiApp
{
    private readonly    GuiDemoView     nativeView;
    private readonly    GuiResources    resources;

    public BasicGuiApp(SmGuiBackend backend, bool addSessionLoop)
    {
        resources = new GuiResources(backend);
        nativeView  = new GuiDemoView(resources, null);
        if (addSessionLoop) {
            Loop = CreateSessionLoop(backend);
        }
    }

    public override void Dispose()
    {
        resources.Dispose();
        
        Loop?.Dispose();
    }
    
    public override void RenderApp(SmBatch batch,  int width, int height)
    {
        Loop?.IterateSessions();
        
        nativeView.RenderView(batch, width, height);
    }
    
    /// <summary>
    /// Starts an HTTP server and creates a session loop that handles all web client sessions on the main thread.<br/>
    /// By calling <see cref="SmSessionLoop.IterateSessions"/> periodically, queued user input (mouse and keyboard) is processed. 
    /// Every client with pending input or a dirty session renders a new frame via <see cref="GuiDemoView.RenderView"/>, which is then sent to the client. 
    /// </summary>
    private SmSessionLoop CreateSessionLoop(SmGuiBackend backend)
    {
        var sessionLoop = SmSessionLoop.Create(new LoopOptions {
            Backend     = new CpuBackend("SmGui - Remote Test", backend),
            ViewFactory = session => new GuiDemoView(resources, session)
        });
        return sessionLoop;
    }
    
    internal const int Port = 8080;
}
