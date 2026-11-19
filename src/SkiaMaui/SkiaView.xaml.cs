using Demos.BasicGui;
using Friflo.SmGui;
using Friflo.SmGui.SkiaSharp;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace SkiaMaui;

public partial class SkiaView : ContentView
{
    private             SkiaSharpGuiBackend?    guiBackend;
    private             SkiaSharpBatch?         batch;
    private             SmGuiApp?               app;
    
    public SkiaView()
    {
        InitializeComponent();
        guiBackend  = new SkiaSharpGuiBackend(new DefaultGuiAssets());
        batch       = guiBackend.CreateBatch();
        app         = new BasicGuiApp(guiBackend);
    }

    public void InvalidateCanvas() => CanvasView.InvalidateSurface();

    private void OnCanvasViewPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.White);
        
        var width = e.Info.Width;
        var height = e.Info.Height;

        // MauiInput.UpdateInput(guiBackend);
        guiBackend?.NewFrame();
        app?.RenderApp(batch!, width, height);
        batch?.DrawCommandList(canvas, 0, 0, width, height);
        // MauiInput.SetMouseCursor(guiBackend);

        /* using var paint = new SKPaint {
            Color = SKColors.Blue,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawCircle(200, 200, 100, paint); */
    }
}