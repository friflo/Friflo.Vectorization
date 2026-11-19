// See https://aka.ms/new-console-template for more information

using System.Net;
using Friflo.SmGui;
using Friflo.SmGui.Http;
using KNI;

// HTTP server for Browser sessions.  nuget: Friflo.SmGui.Http
var webApp = HttpUtils.CreateWebApplication(new IPEndPoint(IPAddress.IPv6Any, 8080));
var sessionHandler = webApp.UseWebSocketGuiSessions();
await webApp.StartAsync();

KniRunner.Run("SmGui", 1280, 1200, sessionHandler, backend => new Demos.BasicGui.BasicGuiApp(backend));
KniRunner.Run("SmDraw",1280,  720, sessionHandler, backend => new Demos.BasicDraw.BasicDrawApp(backend));

await webApp.StopAsync();

public static class KniRunner
{
    public static int Run(string title, int width, int height, WebSessionHandler? sessionHandler, CreateGuiApp createGuiApp)
    {
        createGuiApp = WebSessionHandler.AddSessionLoop(createGuiApp, sessionHandler);
        var app = new GameApp(title, width, height, createGuiApp);
        app.Run();
        
        sessionHandler?.RemoveSessionLoop();
        app.Dispose();
        return 0;
    }
}