// --- GuiTest.cs

using System.Diagnostics;
using System.Net;
using Friflo.SmGui;
using Friflo.SmGui.Headless;
using Friflo.SmGui.Http;
using Friflo.SmGui.Session;
using Friflo.SmGui.WGPU;
using Friflo.WGPU;

namespace TestConsole;

/// <summary>
/// Contains resources shared by all <see cref="GuiView"/>'s.
/// </summary>
public class AppResources : IDisposable
{
    private  readonly   GpuTexture  gpuTexture;
    internal readonly   SmTexture   myTexture;
    internal readonly   SmFont      monocraftFont;
    
    public AppResources (SmGuiBackend backend)
    {
        using var monoTtf   = typeof(SmGuiRenderer).Assembly.GetManifestResourceStream("Tests-Console.Assets.Monocraft.ttf")!;  
        monocraftFont       = backend.CreateTtfFont(monoTtf, 48, 256, 256, 32, 95, "Monocraft");
        Debug.Assert(monocraftFont.maxY == 244);

        // create tile texture
        using var stream = typeof(AppWindow).Assembly.GetManifestResourceStream("Tests-Console.Assets.img.world_tileset.png")!;
        gpuTexture          = backend.LoadTexture(stream, "world_tileset.png").AsGpuTexture(); 
        myTexture           = gpuTexture.CreateView().AsSmTexture();
    }
    
    public void Dispose()
    {
        monocraftFont.Dispose();
        gpuTexture.Dispose();
    }
}


public class SmGuiRenderer : IRenderer
{
    private readonly    WgpuBatch               gpuBatch;
    private readonly    GpuRenderPassDescriptor renderPassDescriptor    = new () { colorAttachments = [ default ] };
    private readonly    PerfLog                 perfLog                 = new();
    private readonly    AppResources            resources;
    private readonly    GuiView                 nativeView;
    private readonly    SmSessionLoop           loop;
    
    public SmGuiBackend? GuiBackend { get; }

    public void OnShutdown()
    {
        loop.Dispose();
        resources.Dispose();
        gpuBatch.Dispose();
    }
    
    public SmGuiRenderer(WgpuHost wgpuHost)
    {
        var wgpuBackend = new WgpuGuiBackend(wgpuHost.Device, new DefaultGuiAssets());
        GuiBackend      = wgpuBackend;
        gpuBatch        = wgpuBackend.CreateBatch(wgpuHost.SwapChainFormat);
        resources       = new AppResources(GuiBackend);
        nativeView      = new GuiView(resources);
        loop            = CreateSessionLoop(GuiBackend, resources);
    }
    
    /// <summary>
    /// Starts an HTTP server and creates a session loop that handles all web client sessions on the main thread.<br/>
    /// By calling <see cref="SmSessionLoop.IterateSessions"/> periodically, queued user input (mouse and keyboard) is processed. 
    /// Every client with pending input or a dirty session renders a new frame via <see cref="GuiView.RenderView"/>, which is then sent to the client. 
    /// </summary>
    private static SmSessionLoop CreateSessionLoop(SmGuiBackend backend, AppResources resources)
    {
        var loop = SmSessionLoop.Create(new LoopOptions {
            Backend     = new CpuBackend("SmGui - Remote Test", backend),
            ViewFactory = session => new GuiView(resources, session)
        });
        // HTTP server for Browser sessions.  nuget: Friflo.SmGui.Http
        var app = HttpUtils.CreateWebApplication(new IPEndPoint(IPAddress.IPv6Any, Port));
        app.UseWebSocketGuiSessions(loop);
        _ = app.StartAsync();
        return loop;
    }
    
    internal const int Port = 8080;
    
    public void OnWindowChanged(int width, int height)
    {
        renderPassDescriptor.colorAttachments[0] = new GpuRenderPassColorAttachment {
            loadOp      = LoadOp.Clear,
            storeOp     = StoreOp.Store,
            clearValue  = new GpuColor(0.6f, 0.61f, 0.62f, 1f)
        };
    }
    
    public void OnFrame(in RenderTarget target)
    {
        _ = gpuBatch.Timer.Enabled;    // not relevant - only smoke test to ensure Timer instance != null
        
        perfLog.Trace(10000);
        
        loop.IterateSessions();
        
        nativeView.RenderView(gpuBatch, target.Width, target.Height);

        gpuBatch.DrawCommandList(target, renderPassDescriptor);
    }
}
