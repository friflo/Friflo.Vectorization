// See https://aka.ms/new-console-template for more information

using System.Net;
using Friflo.SmGui;
using Friflo.SmGui.Http;
using MonoGame;


// HTTP server for Browser sessions.  nuget: Friflo.SmGui.Http
var webApp = HttpUtils.CreateWebApplication(new IPEndPoint(IPAddress.IPv6Any, 8080));
var guiHandler = webApp.UseWebSocketGuiSessions();
await webApp.StartAsync();

MonoGameRunner.Run("SmGui", 1280, 1200, guiHandler, backend => new Demos.BasicGui.BasicGuiApp(backend, true));
MonoGameRunner.Run("SmDraw",1280,  720, guiHandler, backend => new Demos.BasicDraw.BasicDrawApp(backend));

await webApp.StopAsync();

public static class MonoGameRunner
{
    public static int Run(string title, int width, int height, HttpGuiHandler? guiHandler, CreateGuiApp createGuiApp)
    {
        SmGuiApp CreateApp(SmGuiBackend backend) {
            var app = createGuiApp(backend);
            if (guiHandler != null) guiHandler.loop = app.Loop;
            return app;
        }
        var app = new GameApp(title, width, height, CreateApp);
        app.Run();

        app.Dispose();
        return 0;
    }
}