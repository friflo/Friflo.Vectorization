// See https://aka.ms/new-console-template for more information

using System;
using System.Net;
using Friflo.SmGui;
using Friflo.SmGui.Http;
using Friflo.SmGui.WebRTC;
using MonoGame;

var config = new AppConfig();

// HTTP server for Browser sessions.  nuget: Friflo.SmGui.Http
var webApp = HttpUtils.CreateWebApplication(new IPEndPoint(IPAddress.IPv6Any, 8080));
webApp.UseWebSocketGuiSessions(config, new Uri("https://localhost:8080"));
await webApp.StartAsync();

SmSignalingClient.Connect(config, new Uri("https://localhost:8081"), "test-app", AppIdType.Auto);

MonoGameRunner.Run("SmGui", 1280, 1200, config, backend => new Demos.BasicGui.BasicGuiApp(backend));
MonoGameRunner.Run("SmDraw",1280,  720, config, backend => new Demos.BasicDraw.BasicDrawApp(backend));

await webApp.StopAsync();

public static class MonoGameRunner
{
    public static int Run(string title, int width, int height, AppConfig config, CreateGuiApp createGuiApp)
    {
        createGuiApp = config.AddSessionLoop(createGuiApp);
        var app = new GameApp(title, width, height, createGuiApp);
        app.Run();

        config.RemoveSessionLoop();
        app.Dispose();
        return 0;
    }
}