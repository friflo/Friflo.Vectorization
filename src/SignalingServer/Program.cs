using System.Net;
using Friflo.SmGui;
using Friflo.SmGui.Headless;
using Friflo.SmGui.Http;
using Friflo.SmGui.Session;
using SmGui.SignalingServer;


Console.WriteLine("Signaling Server for WebRTC");

var cpuBackend  = new CpuBackend("Signaling Server", new DefaultGuiAssets());
var signaling   = new SmSignaling();

var loop = new SmSessionLoop(cpuBackend, client => new SignalingView(signaling, client));
signaling.StateChanged = loop.SetAppDirty;

// HTTP server for Browser sessions.  nuget: Friflo.SmGui.Http
var app = HttpUtils.CreateWebApplication(new IPEndPoint(IPAddress.IPv6Any, 8081));
app.UseWebSocketGuiSessions(loop);
app.UseSignaling(signaling);
_ = app.StartAsync();
    
loop.Run(); // Blocks Main-Thread

