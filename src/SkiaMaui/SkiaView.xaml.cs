using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace SkiaMaui;

public partial class SkiaView : ContentView
{
    public SkiaView()
    {
        InitializeComponent();
    }

    public void InvalidateCanvas() => CanvasView.InvalidateSurface();

    private void OnCanvasViewPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.White);

        using var paint = new SKPaint
        {
            Color = SKColors.Blue,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        canvas.DrawCircle(200, 200, 100, paint);
    }
}