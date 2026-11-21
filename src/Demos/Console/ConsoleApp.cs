using System;
using System.Threading;
using System.Threading.Tasks;
using Friflo.SmGui;
using Friflo.SmGui.Headless;
using Friflo.SmGui.Session;

namespace Headless;


public class ConsoleApp : IDisposable
{
    private readonly    CpuBackend      guiBackend;
    private readonly    SmGuiApp        app;
    private readonly    SmSessionLoop   loop;

    public ConsoleApp(string title, CreateGuiApp createGuiApp)
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
        _ = Task.Run(ConsoleReader);
        loop.Run();
    }
    
    private async Task ConsoleReader()
    {
        while (true) {
            await SwitchToThreadPool();     // ensure running on ThreadPool worker thread
            await Console.Out.WriteAsync(">");
            var cmd = await Console.In.ReadLineAsync();
            await loop.DispatchAsync();     // continue execution on loop thread to avoid explicit locks and contention
                                            // capture state safely on loop thread
            switch (cmd) {
                case "exit": goto LeaveLoop;
                case "delay":
                    await Console.Out.WriteAsync($"delay (5 sec) ... ");
                    // avoid blocking loop thread - browser sessions stay responsive. Run IO heavy operations on worker thread
                    await Task.Run(() => Task.Delay(5000)); 
                    await Console.Out.WriteLineAsync($" finished");
                    break;
                default:
                    await Console.Out.WriteLineAsync($"invalid cmd: {cmd}");
                    break;
            }
        }
    LeaveLoop:
        await Console.Out.WriteLineAsync($"exiting");
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