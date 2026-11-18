// See https://aka.ms/new-console-template for more information

using Friflo.SmGui;
using KNI;


KniRunner.Run("SmGui", 1280, 1200, backend => new Demos.BasicGui.BasicGuiApp(backend, true));
KniRunner.Run("SmDraw",1280,  720, backend => new Demos.BasicDraw.BasicDrawApp(backend));


public static class KniRunner
{
    public static int Run(string title, int width, int height, CreateGuiApp createGuiApp)
    {
        var app = new GameApp(title, width, height, createGuiApp);
        app.Run();
        app.Dispose();
        return 0;
    }
}