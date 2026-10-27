using System.Net;
using Friflo.SmGui;
using Friflo.SmGui.Headless;
using Friflo.SmGui.Http;
using Friflo.SmGui.Session;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmGui.SignalingServer;



Console.WriteLine("Signaling Server for WebRTC");

var cpuBackend  = new CpuBackend("Signaling Server", new DefaultGuiAssets());
var signaling   = new SmSignaling();

var loop = new SmSessionLoop(cpuBackend, client => new SignalingView(signaling, client));
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
    
loop.RunSync(); // Blocks Main-Thread

