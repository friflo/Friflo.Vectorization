using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace SkiaMaui;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private void OnCanvasViewPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        // Canvas leeren (Weiß)
        canvas.Clear(SKColors.White);

        // Einen blauen Kreis zeichnen, um SkiaSharp zu testen
        using var paint = new SKPaint
        {
            Color = SKColors.Blue,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        canvas.DrawCircle(200, 200, 100, paint);
    }
}