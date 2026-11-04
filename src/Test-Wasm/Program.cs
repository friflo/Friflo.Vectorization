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
partial class Program
{
    private static SmSessionLoop loop;
    
    static void Main(string[] args)
    {
        // runs in Browser-Tab
        Console.WriteLine(".NET WebAssembly Core Runtime initialized.");
    }
    
    [JSExport]
    private static async Task Start()
    {
        var cpuBackend  = new CpuBackend("App", new DefaultGuiAssets());
        return;
        // Create one session loop per App
        loop = new SmSessionLoop(cpuBackend, session => new TestGuiView(session)) {
            UseSecureSessionGuids = false
        };
        var signalingClient = new SmSignalingClient(loop);
        await signalingClient.ConnectAsync(new Uri("wss://localhost:8081/signaling"), "test-app", "auto", CancellationToken.None);
    }
    
    [JSExport]
    private static string ProcessData(string input)
    {
        return $"C# processed: {input}";
    }
}