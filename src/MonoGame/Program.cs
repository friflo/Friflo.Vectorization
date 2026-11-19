// See https://aka.ms/new-console-template for more information

using System.Net;
using Friflo.SmGui;
using Friflo.SmGui.Http;
using MonoGame;


// HTTP server for Browser sessions.  nuget: Friflo.SmGui.Http
var webApp = HttpUtils.CreateWebApplication(new IPEndPoint(IPAddress.IPv6Any, 8080));
var sessionHandler = webApp.UseWebSocketGuiSessions();
await webApp.StartAsync();

MonoGameRunner.Run("SmGui", 1280, 1200, sessionHandler, backend => new Demos.BasicGui.BasicGuiApp(backend));
MonoGameRunner.Run("SmDraw",1280,  720, sessionHandler, backend => new Demos.BasicDraw.BasicDrawApp(backend));

await webApp.StopAsync();

public static class MonoGameRunner
{
    public static int Run(string title, int width, int height, WebSessionHandler? sessionHandler, CreateGuiApp createGuiApp)
    {
        var createSessionApp = WebSessionHandler.CreateSessionAppDelegate(createGuiApp, sessionHandler);
        var app = new GameApp(title, width, height, createSessionApp);
        app.Run();

        app.Dispose();
        return 0;
    }
}