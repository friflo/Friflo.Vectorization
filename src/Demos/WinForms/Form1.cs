using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace WinFormsApp;

public partial class Form1 : Form
{
    private readonly SKControl skControl;

    public Form1() {
        // Set up basic form and SkiaSharp surface
        ClientSize = new Size(800, 450);

        skControl = new SKControl { Dock = DockStyle.Fill };
        skControl.PaintSurface += OnPaintSurface;
        Controls.Add(skControl);
    }

    // Draw a centered circle on the SkiaSharp canvas
    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e) {
        SKCanvas canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.White);

        float cx = e.Info.Width / 2f;
        float cy = e.Info.Height / 2f;
        float radius = Math.Min(cx, cy) * 0.5f;

        using var paint = new SKPaint {
            Color = SKColors.DodgerBlue,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        canvas.DrawCircle(cx, cy, radius, paint);
    }
}