using System.Net;
using Friflo.TmGui;
using Friflo.TmGui.Headless;
using Friflo.TmGui.Http;
using Friflo.TmGui.Session;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TmGui.SignalingServer;



Console.WriteLine("Signaling Server for WebRTC");

var cpuBackend  = new CpuBackend("Signaling Server", new DefaultGuiAssets());
var signaling   = new TmSignaling();

var loop = new TmSessionLoop(false, cpuBackend, info => new SignalingView(signaling, info));
signaling.StateChanged = loop.SetAppDirty;

var builder = Host.CreateDefaultBuilder();

// Disable verbose framework logging for high performance
builder.ConfigureLogging(logging => logging.ClearProviders());

builder.ConfigureWebHostDefaults(webBuilder =>
{
    webBuilder.UseSockets(socketOptions => {
        // deactivate Nagle-algorithm for minimal websocket latency
        socketOptions.NoDelay = true;
    });
    webBuilder.UseKestrel(options => {
        options.Listen(new IPEndPoint(IPAddress.IPv6Any, 8081), listenOptions => {
            listenOptions.UseHttps(); // uses automatically dotnet dev-certs
        });
    });

    webBuilder.Configure(app => {
        app.UseSessionLoop(loop);
        app.UseSignaling(signaling);
    });
});


_ = HttpUtils.Start(builder.Build());
    
loop.StartSync(); // Blocks Main-Thread

