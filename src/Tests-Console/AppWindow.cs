using Friflo.SmGui;
using Friflo.WGPU;
using Friflo.WGPU.SDL3;
using GuiDemo;
using SDL3;
using SmGui.MonoGame;

// ReSharper disable ConvertToLocalFunction
public sealed class AppWindow : Sdl3Window
{
    public static int Run(string title, int width, int height, CreateRenderer createRenderer, CreateGuiApp? createGuiApp = null)
    {
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Gamepad)) throw new Exception($"SDL3 initialization failed: {SDL.GetError()}");
        var window = new AppWindow();
        var opt = new InitOptions{ title = title, width = width, height = height, createRenderer = createRenderer, createGuiApp = createGuiApp };
        return window.EnterAppMain(opt);
    }
    
    public static int RunGuiApp(string title, int width, int height, CreateGuiApp createGuiApp)
    {
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Gamepad)) throw new Exception($"SDL3 initialization failed: {SDL.GetError()}");
        var window = new AppWindow();
        CreateRenderer createRenderer = wgpuHost => new GuiAppRenderer(wgpuHost);
        var opt = new InitOptions{ title = title, width = width, height = height, createRenderer = createRenderer, createGuiApp = createGuiApp };
        return window.EnterAppMain(opt);
    }
    
    public static int RunMonoGame(string title, int width, int height, CreateGuiApp createGuiApp)
    {
        var app = new GameApp(title, width, height, createGuiApp);
        
        app.Run();
        
        app.Dispose();
        return 0;
    }
}