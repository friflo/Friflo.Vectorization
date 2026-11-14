
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
    
    protected override void OnSystemThemeChanged()
    {
        var theme = SDL.GetSystemTheme();
        var image = theme == SDL.SystemTheme.Dark ? "Tests-Console.Assets.wgpu-transparent-dark-128x128.bmp" : 
                                                    "Tests-Console.Assets.wgpu-transparent-light-128x128.bmp";
        using var stream = typeof(Sdl3Window).Assembly.GetManifestResourceStream(image);
        if (stream != null) {
            SetWindowIcon(stream);
        }
    }
}