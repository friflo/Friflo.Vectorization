using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Friflo.SmGui;
using Friflo.SmGui.Headless;
using Friflo.SmGui.Session;

namespace CLI;


public class Cli : IDisposable
{
    private readonly        CpuBackend      guiBackend;
    private readonly        SmGuiApp        app;
    private readonly        SmSessionLoop   loop;
    private static readonly TextWriter      Cout = Console.Out;

    public Cli(string title, CreateGuiApp createGuiApp)
    {
        guiBackend  = new CpuBackend("CLI", new DefaultGuiAssets());
        app         = createGuiApp(guiBackend);
        loop        = app.SessionLoop!;
        Cout.WriteLine($"{title} started");
        Cout.WriteLine($"Web:    {loop.Config.WebAppUrl}");
        Cout.WriteLine($"WebRTC: {loop.Config.WebRtcAppUrl}");
        Help();
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
            await ThreadPoolAwaiter.SwitchToAsync();    // ensure running on ThreadPool worker thread
            await Cout.WriteAsync(">");                 // <- running on worker thread here
            var cmd = await Console.In.ReadLineAsync();
            
            await loop.DispatchAsync();                 // continue execution on loop thread to avoid explicit locks and contention
            await ExecuteCommandAsync(cmd);             // <- now running on main (loop) thread here
        }
        await Cout.WriteLineAsync($"exiting");
        loop.Dispose();
    }
    
    private async Task ExecuteCommandAsync(string? cmd)
    {
        switch (cmd) {
            case null:
            case "exit":    running = false;    return;
            case "":                            return;
            case "help":    Help();             return;
            case "delay":   await Delay();      return;
            default:        InvalidCmd(cmd);    return;
        }
    }
    
    private static void InvalidCmd(string? cmd) {
        Cout.WriteLine($"invalid cmd: {cmd}");
    }
    
    private static async Task Delay()
    {
        await Cout.WriteAsync($"delay (5 sec) - browser sessions stay responsive ... ");
        // avoid blocking loop thread - browser sessions stay responsive. Run IO heavy operations on worker thread
        await Task.Run(() => Thread.Sleep(5000)); // Sleep does not block main thread
        await Cout.WriteLineAsync($"finished");
    }
    
    private static void Help() {
        Cout.WriteLine("commands: exit, delay, help");
    }
}

