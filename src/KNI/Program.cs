// See https://aka.ms/new-console-template for more information

using System;
using System.Net;
using Demos;
using Friflo.SmGui;
using Friflo.SmGui.Http;
using Friflo.SmGui.WebRTC;
using KNI;

var config = new AppConfig();

// HTTP server for Browser sessions.  nuget: Friflo.SmGui.Http
var webApp = HttpUtils.CreateWebApplication(new IPEndPoint(IPAddress.IPv6Any, DemoConfig.HttpsPort));
webApp.UseWebSocketGuiSessions(config);
await webApp.StartAsync();

SmSignalingClient.Connect(config, new Uri(DemoConfig.SignalingUrl), "test-app", "auto");

KniRunner.Run("SmGui", 1280, 1200, config, backend => new Demos.BasicGui.BasicGuiApp(backend));
KniRunner.Run("SmDraw",1280,  720, config, backend => new Demos.BasicDraw.BasicDrawApp(backend));

await webApp.StopAsync();

public static class KniRunner
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