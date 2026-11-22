using System;
using System.Threading;
using System.Threading.Tasks;
using Friflo.SmGui;
using Friflo.SmGui.Headless;
using Friflo.SmGui.Session;

namespace CLI;


public class Cli : IDisposable
{
    private readonly    CpuBackend      guiBackend;
    private readonly    SmGuiApp        app;
    private readonly    SmSessionLoop   loop;

    public Cli(string title, CreateGuiApp createGuiApp)
    {
        guiBackend  = new CpuBackend("Console", new DefaultGuiAssets());
        app         = createGuiApp(guiBackend);
        loop        = app.SessionLoop!;
        Console.Out.WriteLine($"{title} started");
        Console.Out.WriteLine($"Web:    {loop.Config.WebAppUrl}");
        Console.Out.WriteLine($"WebRTC: {loop.Config.WebRtcAppUrl}");
    }
    
    public void Dispose()
    {
        app.Dispose();
        guiBackend.Dispose();
    }

    public void Run() {
        _ = Task.Run(ReadConsoleLoopAsync);
        loop.Run();
    }
    
    private bool running = true;
    
    private async Task ReadConsoleLoopAsync()
    {
        while (running) {
            await SwitchToThreadPool();     // ensure running on ThreadPool worker thread
            await Console.Out.WriteAsync(">");
            var command = await Console.In.ReadLineAsync();
            
            await loop.DispatchAsync();     // continue execution on loop thread to avoid explicit locks and contention
                                            // <- now running on main (loop) thread here
            await ExecuteCommandAsync(command);
        }
        await Console.Out.WriteLineAsync($"exiting");
    }
    
    private async Task ExecuteCommandAsync(string? cmd)
    {
        var cout = Console.Out;
        switch (cmd) {
            case "exit":
                running = false;
                return;
            case "delay":
                await cout.WriteAsync($"delay (5 sec) ... ");
                // avoid blocking loop thread - browser sessions stay responsive. Run IO heavy operations on worker thread
                await Task.Run(() => Task.Delay(5000)); 
                await cout.WriteLineAsync($"finished");
                break;
            default:
                await cout.WriteLineAsync($"invalid cmd: {cmd}");
                break;
        }
    }
    
    private static ThreadPoolAwaiter SwitchToThreadPool() => default;

    private readonly struct ThreadPoolAwaiter : System.Runtime.CompilerServices.INotifyCompletion
    {
        public bool IsCompleted => false; // always force async continuation so it cannot run inline on the current thread

        public void OnCompleted(Action continuation) =>
            ThreadPool.UnsafeQueueUserWorkItem(static state => ((Action)state!)(), continuation);

        public void GetResult() { }

        public ThreadPoolAwaiter GetAwaiter() => this;
    }
}