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
            var cmd = await Console.In.ReadLineAsync();
            if (cmd == "exit") break;
            await Console.Out.WriteLineAsync($"> {cmd}");
            await loop.DispatchAsync();     // continue execution on loop thread to avoid explicit locks and contention
                                            // capture state safely on loop thread
            
            await SwitchToThreadPool();     // switch back to ThreadPool worker thread
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