using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Friflo.SmGui;
using Friflo.SmGui.Headless;
using Friflo.SmGui.Session;
using Friflo.SmGui.WebRTC;
using Demos.Wasm;


// requires one-time activation of developer certificate:
//   dotnet dev-certs https --trust

[SupportedOSPlatform("browser")]
internal static class Program
{
    // called from JS via:  await runtime.runMain();
    private static async Task Main(string[] _)
    {
        var cpuBackend  = new CpuBackend("App", new DefaultGuiAssets()) {
            Runtime             = new WasmRuntime(),
            RuntimeEventHandler = WasmRuntimeEventHandler.Instance
        };

        // Create one session loop per App
        var loop = SmSessionLoop.Create(new LoopOptions {
            Backend     = cpuBackend,
            ViewFactory = session => new TestGuiView(session)
        });
        
        var config = new AppConfig(loop);
        await SmSignalingClient.ConnectAsync(config, new Uri("https://localhost:8081/?app=test-app"), AppIdType.Auto);
        
        JSHost.GlobalThis.SetProperty("onAppStarted", loop.Config.AppId);
    }
}