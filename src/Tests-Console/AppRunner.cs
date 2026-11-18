using Friflo.SmGui;
using Friflo.WGPU;
using Friflo.WGPU.SDL3;
using SDL3;

// ReSharper disable ConvertToLocalFunction
namespace TestConsole;


public static class AppRunner
{
    public static int Run(string title, int width, int height, CreateRenderer createRenderer, CreateGuiApp? createGuiApp = null)
    {
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Gamepad)) throw new Exception($"SDL3 initialization failed: {SDL.GetError()}");
        var window = new Sdl3Window();
        var opt = new InitOptions{ title = title, width = width, height = height, createRenderer = createRenderer, createGuiApp = createGuiApp };
        return window.EnterAppMain(opt);
    }
}