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

        // Create one session loop per App
        loop = new SmSessionLoop(cpuBackend, session => new TestGuiView(session)) {
            UseSecureSessionGuids = false
        };
        var signalingClient = new SmSignalingClient(loop);
        await signalingClient.ConnectAsync(new Uri("wss://localhost:8081/signaling"), "test-app", "auto", CancellationToken.None);
        _ = signalingClient.RunAsync();
        
        
        // Start the infinite loop asynchronously without blocking the browser thread
        _ = RunLoopAsync(loop);
    }
    
    private static async Task RunLoopAsync(SmSessionLoop sessionLoop)
    {
        var interval = TimeSpan.FromMilliseconds(16);

        try
        {
            while (true)
            {
                // Perform one iteration of the session loop
                sessionLoop.IterateSessions();

                // Wait non-blockingly for the next tick, allowing the browser to render and process events
                await Task.Delay(interval);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in session loop: {ex.Message}");
        }
    }
    
    [JSExport]
    private static string ProcessData(string input)
    {
        return $"C# processed: {input}";
    }
}