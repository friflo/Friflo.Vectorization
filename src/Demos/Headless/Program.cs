// See https://aka.ms/new-console-template for more information

using System;
using System.Net;
using Friflo.SmGui;
using Friflo.SmGui.Http;
using Friflo.SmGui.WebRTC;
using Headless;

var config = new AppConfig();

// WebSocket browser sessions: this app hosts the page.  nuget: Friflo.SmGui.Http
var webApp = HttpUtils.CreateWebApplication(new IPEndPoint(IPAddress.IPv6Any, 8080));
webApp.UseWebSocketGuiSessions(config, new Uri("https://localhost:8080"));
await webApp.StartAsync();

// WebRTC browser sessions: signaling server hosts the page.  nuget: Friflo.SmGui.WebRTC
SmSignalingClient.Connect(config, new Uri("https://localhost:8081/?app=test-app"), AppIdType.Auto);

HeadlessRunner.Run("SmGui",  config, backend => new Demos.BasicGui.BasicGuiApp(backend));
HeadlessRunner.Run("SmDraw", config, backend => new Demos.BasicDraw.BasicDrawApp(backend));

await webApp.StopAsync();

public static class HeadlessRunner
{
    public static int Run(string title, AppConfig config, CreateGuiApp createGuiApp)
    {
        createGuiApp = config.AddSessionLoop(createGuiApp);
        var app = new ConsoleApp(title, createGuiApp);
        
        app.Run();

        config.RemoveSessionLoop();
        app.Dispose();
        return 0;
    }
}