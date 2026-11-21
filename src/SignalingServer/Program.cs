using System.Net;
using Friflo.SmGui;
using Friflo.SmGui.Headless;
using Friflo.SmGui.Http;
using Friflo.SmGui.Session;
using SmGui.SignalingServer;


Console.WriteLine("Signaling Server for WebRTC");
Console.WriteLine($"https://localhost:8081/");

var cpuBackend  = new CpuBackend("Signaling Server", new DefaultGuiAssets());
var signaling   = new SmSignaling();

var loop = SmSessionLoop.Create(new LoopOptions {
    Backend     = cpuBackend,
    ViewFactory = session => new SignalingView(signaling, session)
});
signaling.StateChanged = loop.SetAppDirty;

var config = new AppConfig(loop);

// HTTP server for Browser sessions.  nuget: Friflo.SmGui.Http
var webApp = HttpUtils.CreateWebApplication(new IPEndPoint(IPAddress.IPv6Any, 8081));
webApp.UseWebSocketGuiSessions(config, new Uri($"https://localhost:{8081}"));
webApp.UseSignaling(signaling);
_ = webApp.StartAsync();
    
loop.Run(); // Blocks Main-Thread

