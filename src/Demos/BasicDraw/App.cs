using System.Diagnostics;
using System.Net;
using Friflo.SmGui;
using Friflo.SmGui.Headless;
using Friflo.SmGui.Http;
using Friflo.SmGui.Session;
using Microsoft.AspNetCore.Builder;

namespace Demos.BasicDraw;


public sealed class BasicDrawApp : SmGuiApp
{
    private readonly    BasicDrawView   nativeView;
    private readonly    SmSessionLoop?  loop;
    private             WebApplication? webApp;
    // --- resources
    internal readonly   SmTexture       myTexture;
    internal readonly   SmFont          monocraftFont;
    
    public BasicDrawApp(SmGuiBackend backend, bool addSessionLoop)
    {
        nativeView  = new BasicDrawView(this, null);
        if (addSessionLoop) {
            loop = CreateSessionLoop(backend);
        }
        // --- resources
        var assembly = typeof(BasicDrawApp).Assembly;
        
        using var monoTtf   = assembly.GetManifestResourceStream("Demos.Assets.Monocraft.ttf")!;  
        monocraftFont       = backend.CreateTtfFont(monoTtf, 48, 256, 256, 32, 95, "Monocraft");
        Debug.Assert(monocraftFont.maxY == 244);

        // create tile texture
        using var stream    = assembly.GetManifestResourceStream("Demos.Assets.world_tileset.png")!;
        myTexture           = backend.LoadTexture(stream, "world_tileset.png");
    }

    public override void Dispose()
    {
        // --- resources
        monocraftFont.Dispose();
        myTexture.Dispose();
        
        webApp?.StopAsync();
        loop?.Dispose();
    }
    
    public override void RenderApp(SmBatch batch,  int width, int height)
    {
        loop?.IterateSessions();
        
        nativeView.RenderView(batch, width, height);
    }
    
    /// <summary>
    /// Starts an HTTP server and creates a session loop that handles all web client sessions on the main thread.<br/>
    /// By calling <see cref="SmSessionLoop.IterateSessions"/> periodically, queued user input (mouse and keyboard) is processed. 
    /// Every client with pending input or a dirty session renders a new frame via <see cref="BasicDrawView.RenderView"/>, which is then sent to the client. 
    /// </summary>
    private SmSessionLoop CreateSessionLoop(SmGuiBackend backend)
    {
        var sessionLoop = SmSessionLoop.Create(new LoopOptions {
            Backend     = new CpuBackend("SmGui - Remote Test", backend),
            ViewFactory = session => new BasicDrawView(this, session)
        });
        // HTTP server for Browser sessions.  nuget: Friflo.SmGui.Http
        webApp = HttpUtils.CreateWebApplication(new IPEndPoint(IPAddress.IPv6Any, Port));
        webApp.UseWebSocketGuiSessions(sessionLoop);
        _ = webApp.StartAsync();
        return sessionLoop;
    }
    
    internal const int Port = 8080;
}
