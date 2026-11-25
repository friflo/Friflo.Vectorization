// See https://aka.ms/new-console-template for more information

using System;
using System.Net;
using Friflo.SmGui;
using Friflo.SmGui.Http;
using Friflo.SmGui.WebRTC;
using MonoGame;

var config = new AppConfig();

// WebSocket browser sessions: this app hosts the page.  nuget: Friflo.SmGui.Http
var webApp = HttpUtils.CreateWebApplication(new IPEndPoint(IPAddress.IPv6Any, 8080));
webApp.UseWebSocketGuiSessions(config, new Uri("https://localhost:8080"));
await webApp.StartAsync();

// WebRTC browser sessions: signaling server hosts the page.  nuget: Friflo.SmGui.WebRTC
SmSignalingClient.Connect(config, new Uri("https://localhost:8081/?app=test-app"), AppIdType.Auto);

MonoGameRunner.Run("BasicGui",  1280, 1200, config, backend => new Demos.BasicGui.BasicGuiApp(backend));
MonoGameRunner.Run("BasicDraw", 1280, 1200, config, backend => new Demos.BasicDraw.BasicDrawApp(backend));

await webApp.StopAsync();


public static class MonoGameRunner
{
    public static int Run(string title, int width, int height, AppConfig config, CreateGuiApp createGuiApp)
    {
        createGuiApp = config.AddSessionLoop(createGuiApp);
        var app = new GameWindow(title, width, height, createGuiApp);
        app.Run();

        config.RemoveSessionLoop();
        app.Dispose();
        return 0;
    }
}