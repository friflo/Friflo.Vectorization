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
        guiBackend  = new CpuBackend("CLI", new DefaultGuiAssets());
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
            await SwitchToThreadPool();         // ensure running on ThreadPool worker thread
            await Console.Out.WriteAsync(">");  // <- running on worker thread here
            var command = await Console.In.ReadLineAsync();
            
            await loop.DispatchAsync();         // continue execution on loop thread to avoid explicit locks and contention
            await ExecuteCommandAsync(command); // <- now running on main (loop) thread here
        }
        await Console.Out.WriteLineAsync($"exiting");
        loop.Dispose();
    }
    
    private async Task ExecuteCommandAsync(string? cmd)
    {
        var cout = Console.Out;
        switch (cmd) {
            case "":
                return;
            case "exit":
                running = false;
                return;
            case "delay":
                await cout.WriteAsync($"delay (5 sec) ... ");
                // avoid blocking loop thread - browser sessions stay responsive. Run IO heavy operations on worker thread
                await Task.Run(() => Thread.Sleep(5000)); // Sleep does not block main thread
                await cout.WriteLineAsync($"finished");
                return;
            default:
                await cout.WriteLineAsync($"invalid cmd: {cmd}");
                return;
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