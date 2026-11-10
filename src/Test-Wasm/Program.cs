using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Friflo.SmGui;
using Friflo.SmGui.Headless;
using Friflo.SmGui.Session;
using Friflo.SmGui.WebRTC;
using TuiTerminal;


// requires one-time activation of developer certificate:
//   dotnet dev-certs https --trust

[SupportedOSPlatform("browser")]
internal static class Program
{
    // called from JS via:  await runtime.runMain();
    private static async Task Main(string[] _)
    {
        var cpuBackend  = new CpuBackend("App", new DefaultGuiAssets());

        // Create one session loop per App
        var loop = new SmSessionLoop(cpuBackend, session => new TestGuiView(session)) {
            EventReady              = new WasmEventReady(),
            Runtime                 = new WasmRuntime(),
            UseSecureSessionGuids   = false
        };
        await loop.UseWebRtcGuiSessionsAsync(new Uri("wss://localhost:8081/signaling"), "test-app", "auto");
        
        JSHost.GlobalThis.SetProperty("onAppStarted", loop.appId);
    }
}