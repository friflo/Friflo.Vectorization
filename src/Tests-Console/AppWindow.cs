
using Friflo.WGPU;
using SDL3;
using TestConsole;

public sealed class AppWindow : Sdl3Window
{
    public static int Run(string title, int width, int height, Func<WgpuHost, IRenderer> createRenderer)
    {
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Gamepad)) throw new Exception($"SDL3 initialization failed: {SDL.GetError()}");
        var app = new AppWindow();
        var opt = new InitOptions{ title = title, width = width, height = height, createRenderer = createRenderer };
        return app.EnterAppMain(opt);
    }
}