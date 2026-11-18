// See https://aka.ms/new-console-template for more information

using Friflo.SmGui;
using MonoGame;


MonoGameRunner.Run("SmGui", 1280, 1200, backend => new Demos.BasicGui.BasicGuiApp(backend, true));
MonoGameRunner.Run("SmDraw",1280,  720, backend => new Demos.BasicDraw.BasicDrawApp(backend));


public static class MonoGameRunner
{
    public static int Run(string title, int width, int height, CreateGuiApp createGuiApp)
    {
        var app = new GameApp(title, width, height, createGuiApp);
        app.Run();
        app.Dispose();
        return 0;
    }
}