using System;
using Friflo.SmGui;
using Friflo.SmGui.Raylib;
using Raylib_cs;
using Rl = Raylib_cs.Raylib;

// ReSharper disable CheckNamespace
// ReSharper disable ConvertToPrimaryConstructor
namespace Raylib;

// backend:  https://github.com/raylib-cs/raylib-cs
/// <summary>
/// Minimal implementation to create a <see href="https://github.com/raylib-cs/raylib-cs#hello-world">raylib window</see>.<br/>
/// You can copy this class for customization or for using raylib-specific APIs.
/// </summary>
public class RaylibWindow : IDisposable
{
    private             RaylibGuiBackend?   guiBackend;
    private             RaylibBatch?        batch;
    private             SmGuiApp?           app;
    private readonly    CreateGuiApp        createGuiApp;

    public RaylibWindow(string title, int width, int height, CreateGuiApp createGuiApp)
    {
        this.createGuiApp = createGuiApp;
        Rl.SetConfigFlags(ConfigFlags.ResizableWindow);
        Rl.InitWindow(width, height, title);
    }
    
    private void SetWindowIcon()
    {
        using var stream  = typeof(RaylibGuiBackend).Assembly.GetManifestResourceStream("Friflo.SmGui.Raylib.Assets.raylib-cs.png")!;
        var texture = guiBackend!.LoadTexture(stream, "raylib-cs").AsGpuTexture();
        var image = Rl.LoadImageFromTexture(texture.native);
        Rl.SetWindowIcon(image);
    }

    public void Run()
    {
        guiBackend  = new RaylibGuiBackend(new DefaultGuiAssets());
        batch       = guiBackend.CreateBatch();
        app         = createGuiApp(guiBackend);
        guiBackend.SetClientActions(new RaylibClientAction());
        SetWindowIcon();

        while (!Rl.WindowShouldClose())
        {
            Draw();
        }
    }

    private void Draw()
    {
        Rl.BeginDrawing();
        var color = app?.ClearValue ?? 0xBA64D2ff; // 0xACACACFF / 0xE4B9F1FF
        Rl.ClearBackground(color.AsRaylibColor());

        var width   = Rl.GetScreenWidth();
        var height  = Rl.GetScreenHeight();
        RaylibInput.UpdateInput(guiBackend);
        guiBackend?.NewFrame();
        app?.RenderApp(batch!, width, height);
        if (!Rl.IsWindowReady()) {
            return;
        }
        batch?.DrawCommandList(0, 0, width, height);
        RaylibInput.SetMouseCursor(guiBackend);
        
        Rl.EndDrawing();
    }

    public void Dispose()
    {
        if (Rl.IsWindowReady()) {
            Rl.CloseWindow();
        }
        app?.Dispose();
        app = null;
        batch?.Dispose();
        batch = null;
        guiBackend?.Dispose();
        guiBackend = null;
    }
}