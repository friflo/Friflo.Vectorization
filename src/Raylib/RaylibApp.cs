using System;
using Friflo.SmGui;
using Friflo.SmGui.Raylib;
using Raylib_cs;

// ReSharper disable ConvertToPrimaryConstructor
namespace RaylibDemo;

/// <summary>
/// Minimal implementation to create a <see href="https://github.com/raylib-cs/raylib-cs">raylib window</see>.<br/>
/// You can copy this class for customization or for using raylib-specific APIs.
/// </summary>
public class RaylibApp : IDisposable
{
    private             RaylibGuiBackend?   guiBackend;
    private             RaylibBatch?        batch;
    private             SmGuiApp?           app;
    private readonly    CreateGuiApp        createGuiApp;
    private readonly    string              title;
    private readonly    int                 initialWidth;
    private readonly    int                 initialHeight;

    public RaylibApp(string title, int width, int height, CreateGuiApp createGuiApp)
    {
        this.createGuiApp   = createGuiApp;
        this.title          = title;
        initialWidth        = width;
        initialHeight       = height;
    }

    public void Run()
    {
        // Enable resizable window before creating the Raylib context.
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(initialWidth, initialHeight, title);

        guiBackend  = new RaylibGuiBackend(new DefaultGuiAssets());
        batch       = guiBackend.CreateBatch();
        app         = createGuiApp(guiBackend);

        while (!Raylib.WindowShouldClose())
        {
            Draw();
        }
        Raylib.CloseWindow();
    }

    private void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color32.CornflowerBlue.AsRaylibColor());

        var width   = Raylib.GetScreenWidth();
        var height  = Raylib.GetScreenHeight();
        // RaylibInput.UpdateInput(guiBackend);
        guiBackend?.NewFrame();
        app?.RenderApp(batch!, width, height);
        batch?.DrawCommandList(0, 0, width, height);
        // RaylibInput.SetMouseCursor(guiBackend);

        Raylib.EndDrawing();
    }

    public void Dispose()
    {
        app?.Dispose();
        app = null;
        batch?.Dispose();
        batch = null;
        guiBackend?.Dispose();
        guiBackend = null;
    }
}