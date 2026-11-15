using Friflo.SmGui;
using Friflo.WGPU;
using Friflo.WGPU.SDL3;
using GuiDemo;
using SDL3;

// ReSharper disable ConvertToLocalFunction
public sealed class AppWindow : Sdl3Window
{
    public static int Run(string title, int width, int height, CreateRenderer createRenderer, CreateGuiApp? createGuiApp = null)
    {
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Gamepad)) throw new Exception($"SDL3 initialization failed: {SDL.GetError()}");
        var app = new AppWindow();
        var opt = new InitOptions{ title = title, width = width, height = height, createRenderer = createRenderer, createGuiApp = createGuiApp };
        return app.EnterAppMain(opt);
    }
    
    public static int RunGuiApp(string title, int width, int height, CreateGuiApp? createGuiApp = null)
    {
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Gamepad)) throw new Exception($"SDL3 initialization failed: {SDL.GetError()}");
        var app = new AppWindow();
        CreateRenderer createRenderer = wgpuHost => new GuiAppRenderer(wgpuHost);
        var opt = new InitOptions{ title = title, width = width, height = height, createRenderer = createRenderer, createGuiApp = createGuiApp };
        return app.EnterAppMain(opt);
    }
}