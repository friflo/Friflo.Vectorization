using System;
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
        guiBackend  = new CpuBackend("CPU", new DefaultGuiAssets());
        app         = createGuiApp(guiBackend);
        loop        = app.SessionLoop!;
        Console.Out.WriteLine($"{title} started");
        Console.Out.WriteLine($"Web:    {loop.Config.WebAppUrl}");
        Console.Out.WriteLine($"WebRTC: {loop.Config.WebRtcAppUrl}");
    }

    public void Run() {
        loop.Run();
    }

    public void Dispose()
    {
        app.Dispose();
        guiBackend.Dispose();
    }
}