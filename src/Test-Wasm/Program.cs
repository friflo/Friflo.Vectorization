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
static partial class Program
{
    static void Main(string[] _)
    {
        // runs in Browser-Tab
        Console.WriteLine(".NET WebAssembly Core Runtime initialized.");
    }
    
    [JSExport]
    private static async Task<string> Start()
    {
        var cpuBackend  = new CpuBackend("App", new DefaultGuiAssets());

        // Create one session loop per App
        var loop = new SmSessionLoop(cpuBackend, session => new TestGuiView(session)) {
            UseSecureSessionGuids = false
        };
        await loop.UseWebRtcGuiSessionsAsync(new Uri("wss://localhost:8081/signaling"), "test-app", "auto");
        
        // Start the infinite loop asynchronously without blocking the browser thread
        _ = RunLoopAsync(loop);
        
        return loop.appId!;
    }
    
    private static async Task RunLoopAsync(SmSessionLoop sessionLoop)
    {
        int count = 0;

        try
        {
            while (true)
            {
                Console.WriteLine($"IterateSessions {count++}");
                // Perform one iteration of the session loop
                await sessionLoop.IterateSessionsAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in session loop: {ex.Message}");
        }
    }
}