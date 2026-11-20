// See https://aka.ms/new-console-template for more information

using System.Net;
using Demos;
using Friflo.SmGui;
using Friflo.SmGui.Http;
using Raylib;


var sessionHandler = new SessionHandlers();

// HTTP server for Browser sessions.  nuget: Friflo.SmGui.Http
var webApp = HttpUtils.CreateWebApplication(new IPEndPoint(IPAddress.IPv6Any, DemoConfig.HttpsPort));
webApp.UseWebSocketGuiSessions(sessionHandler);
await webApp.StartAsync();

RaylibRunner.Run("SmGui", 1280, 1200, sessionHandler, backend => new Demos.BasicGui.BasicGuiApp(backend));
RaylibRunner.Run("SmDraw",1280,  720, sessionHandler, backend => new Demos.BasicDraw.BasicDrawApp(backend));

await webApp.StopAsync();

public static class RaylibRunner
{
    public static int Run(string title, int width, int height, SessionHandlers? sessionHandlers, CreateGuiApp createGuiApp)
    {
        createGuiApp = sessionHandlers?.AddSessionLoop(createGuiApp) ?? createGuiApp;
        var app = new RaylibApp(title, width, height, createGuiApp);
        app.Run();

        sessionHandlers?.RemoveSessionLoop();
        app.Dispose();
        return 0;
    }
}